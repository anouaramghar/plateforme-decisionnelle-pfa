"""
Loads real student data from PFA_DW (Data Warehouse) for ML training.

Updated to support 5 features: moyenne_generale, taux_absence, nb_modules,
ecart_type_modules (std dev across modules), and nb_echecs_anterieurs
(prior-period failure count, computed in pandas after the SQL load).

If the DW is unreachable or empty, returns None so callers can fall back
to synthetic data.
"""

import os
import re
import logging
from contextlib import closing

import pandas as pd
import numpy as np

from features import FEATURE_COLS

logger = logging.getLogger(__name__)

SESSIONS_PER_MODULE = 32

_PWD_RE = re.compile(r"(?i)(PWD|PASSWORD)\s*=\s*[^;]*")


def _safe_error(exc: BaseException) -> str:
    return _PWD_RE.sub(r"\1=***", str(exc))


def _get_connection_string() -> str | None:
    conn_str = os.getenv("DW_CONNECTION_STRING") or os.getenv("COPILOT_DW_READONLY_CONN")
    if not conn_str:
        logger.info("DW_CONNECTION_STRING not set — will use synthetic data.")
        return None
    if "driver=" not in conn_str.lower():
        conn_str = f"Driver={{ODBC Driver 18 for SQL Server}};{conn_str}"
    conn_str = re.sub(r"(?i)TrustServerCertificate\s*=\s*True", "TrustServerCertificate=yes", conn_str)
    return conn_str


def _compute_period_key(annee: str, semestre: str) -> float:
    match = re.search(r"\d{4}", annee)
    if not match:
        return 0.0
    year = float(match.group())
    sem_match = re.search(r"(?:S|Semestre\s*)([1-9])", str(semestre), re.IGNORECASE)
    sem_val = float(sem_match.group(1)) / 10 if sem_match else 0.0
    return year + sem_val


def _compute_nb_echecs_anterieurs(df: pd.DataFrame) -> pd.Series:
    """
    For each student-period, count the number of PRIOR periods where
    the student's average grade was < 10.
    """
    ordered = df.sort_values(["EtudiantId", "period_key"]).copy()
    ordered["failed_this"] = (ordered["moyenne_generale"] < 10.0).astype(int)
    ordered["nb_echecs_anterieurs"] = (
        ordered.groupby("EtudiantId")["failed_this"]
        .cumsum()
        .groupby(ordered["EtudiantId"])
        .shift(1, fill_value=0)
        .clip(0, 5)
        .astype(int)
    )
    return ordered.sort_index()["nb_echecs_anterieurs"]


def _build_base_query(extra_select: str = "", extra_group: str = "") -> str:
    """Returns the common SQL query skeleton used by all loaders."""
    return f"""
        SELECT
            fn.EtudiantKey AS EtudiantId,
            dt.Annee,
            dt.Semestre,
            de.Matricule,
            AVG(fn.NoteFinale)                          AS moyenne_generale,
            SUM(fn.NbAbsences) * 1.0
                / NULLIF(COUNT(DISTINCT fn.ModuleKey) * {SESSIONS_PER_MODULE}, 0) AS taux_absence,
            COUNT(DISTINCT fn.ModuleKey)                AS nb_modules,
            STDEV(fn.NoteFinale)                        AS ecart_type_modules
            {extra_select}
        FROM      FaitNotes   fn
        JOIN      DimEtudiant de ON de.EtudiantKey = fn.EtudiantKey
        JOIN      DimTemps    dt ON dt.TempsKey = fn.TempsKey
        WHERE     fn.NoteFinale IS NOT NULL
        GROUP BY  fn.EtudiantKey, de.Matricule, dt.Annee, dt.Semestre
        {extra_group}
        HAVING    COUNT(DISTINCT fn.ModuleKey) >= 1
    """


def _process_raw_data(df: pd.DataFrame, min_rows: int = 30) -> pd.DataFrame | None:
    """Common post-processing: clip, sort, add period_key, validate."""
    if len(df) < min_rows:
        logger.warning("DW has only %d rows — too few for reliable training.", len(df))
        return None

    df["taux_absence"] = df["taux_absence"].clip(0, 1)
    df["ecart_type_modules"] = df["ecart_type_modules"].fillna(0).clip(0, 8)
    df["period_key"] = df.apply(
        lambda row: _compute_period_key(str(row["Annee"]), str(row["Semestre"])), axis=1
    )
    df = df.sort_values(["EtudiantId", "period_key"])
    return df


def load_risk_data() -> pd.DataFrame | None:
    """
    Loads training data for the risk classifier from PFA_DW.
    Returns DataFrame with columns: EtudiantId, period_key, plus FEATURE_COLS and at_risk.
    """
    conn_str = _get_connection_string()
    if conn_str is None:
        return None

    try:
        import pyodbc
        query = _build_base_query()
        with closing(pyodbc.connect(conn_str)) as conn:
            df = pd.read_sql(query, conn)

        df = _process_raw_data(df)
        if df is None:
            return None

        # Compute at_risk: 1 if the NEXT period average < 10
        df["failed"] = (df["moyenne_generale"] < 10.0).astype(int)
        df["future_failed"] = df.groupby("EtudiantId")["failed"].shift(-1)
        df = df.dropna(subset=["future_failed"]).copy()
        df["at_risk"] = df["future_failed"].astype(int)

        if df["at_risk"].nunique() < 2:
            logger.warning("DW data has only one class after temporal lagging — fallback to synthetic.")
            return None

        df["nb_echecs_anterieurs"] = _compute_nb_echecs_anterieurs(df)

        cols = ["EtudiantId", "period_key"] + FEATURE_COLS + ["at_risk"]
        df = df[cols]

        logger.info("Loaded %d student-periods from DW for risk training "
                    "(%.1f%% at risk, %d features).",
                    len(df), df["at_risk"].mean() * 100, len(FEATURE_COLS))
        return df

    except Exception as e:
        logger.warning("Could not load risk data from DW: %s — falling back to synthetic.", _safe_error(e))
        return None


def load_clustering_data() -> pd.DataFrame | None:
    """
    Loads unlabeled data for K-Means clustering from PFA_DW.
    """
    df = load_risk_data()
    if df is None:
        return None
    return df[FEATURE_COLS]


def load_forecast_data() -> pd.DataFrame | None:
    """
    Loads training data for grade forecasting from PFA_DW.
    Returns DataFrame with columns: EtudiantId, period_key,
    FORECAST features, and note_finale.
    """
    conn_str = _get_connection_string()
    if conn_str is None:
        return None

    try:
        import pyodbc
        query = _build_base_query()
        with closing(pyodbc.connect(conn_str)) as conn:
            df = pd.read_sql(query, conn)

        df = _process_raw_data(df)
        if df is None:
            return None

        # Compute future grade
        df["future_moyenne"] = df.groupby("EtudiantId")["moyenne_generale"].shift(-1)
        df = df.dropna(subset=["future_moyenne"]).copy()

        df["nb_echecs_anterieurs"] = _compute_nb_echecs_anterieurs(df)

        df = df.rename(columns={
            "moyenne_generale": "moyenne_actuelle",
            "future_moyenne": "note_finale",
        })

        forecast_cols = [
            "moyenne_actuelle", "taux_absence", "nb_modules",
            "ecart_type_modules", "nb_echecs_anterieurs",
        ]
        cols = ["EtudiantId", "period_key"] + forecast_cols + ["note_finale"]
        df = df[cols]

        logger.info("Loaded %d student-periods from DW for forecast training.", len(df))
        return df

    except Exception as e:
        logger.warning("Could not load forecast data from DW: %s — falling back to synthetic.", _safe_error(e))
        return None
