"""
Loads real student data from PFA_DW (Data Warehouse) for ML training.

This module connects to the SQL Server Data Warehouse and queries the star
schema (FaitNotes + dimension tables) to build training DataFrames.

If the DW is unreachable or empty (first boot, no ETL yet), returns None
so callers can fall back to synthetic data.

Connection string is built from the DW_CONNECTION_STRING env var,
passed via docker-compose.yml.
"""

import os
import re
import logging
from contextlib import closing

import pandas as pd

logger = logging.getLogger(__name__)

# Scheduled sessions per module per semester — turns an absence-hour count into a
# rate. ponytail: single school-wide constant; if modules ever differ in volume,
# carry it on DimModule and divide per-module. Keep in sync with the backend's
# AcademicPeriod.SessionsPerModule.
SESSIONS_PER_MODULE = 32

# Matches PWD=... / Password=... up to the next ; or end-of-string. Used to
# scrub the DB password out of pyodbc exception messages — pyodbc tends to
# echo the full connection string back in its error text, and we do not want
# the SQL Server password ending up in container logs or shipped to Sentry.
_PWD_RE = re.compile(r"(?i)(PWD|PASSWORD)\s*=\s*[^;]*")


def _safe_error(exc: BaseException) -> str:
    """Returns str(exc) with any embedded DB password redacted."""
    return _PWD_RE.sub(r"\1=***", str(exc))


def _get_connection_string() -> str | None:
    """Returns the ODBC connection string or None if not configured."""
    conn_str = os.getenv("DW_CONNECTION_STRING")
    if not conn_str:
        logger.info("DW_CONNECTION_STRING not set — will use synthetic data.")
        return None
    return conn_str


def _compute_period_key(annee: str, semestre: str) -> float:
    """Converts Annee (e.g. 2025/2026) and Semestre (S1/S2) to a sortable float."""
    match = re.search(r"\d{4}", annee)
    if not match:
        return 0.0
    year = float(match.group())
    sem_val = 0.1 if "S1" in str(semestre).upper() or "1" in str(semestre) else 0.2
    return year + sem_val


def _process_raw_risk_data(df: pd.DataFrame) -> pd.DataFrame | None:
    """Processes aggregated student-period data to create features and future targets for risk model."""
    if len(df) < 30:
        logger.warning("DW has only %d students — too few for reliable training. "
                       "Falling back to synthetic data.", len(df))
        return None

    # Clip taux_absence to [0, 1]
    df["taux_absence"] = df["taux_absence"].clip(0, 1)

    # Convert Annee and Semestre to period_key
    df["period_key"] = df.apply(lambda row: _compute_period_key(str(row["Annee"]), str(row["Semestre"])), axis=1)

    # Sort by student and period
    df = df.sort_values(["EtudiantId", "period_key"])

    # Define failed in current period
    df["failed"] = (df["moyenne_generale"] < 10.0).astype(int)

    # Shift failed backward by -1 per student (so prior period predicts next-period failure)
    df["future_failed"] = df.groupby("EtudiantId")["failed"].shift(-1)

    # Drop last period because it has no future label
    df = df.dropna(subset=["future_failed"]).copy()

    # Define at_risk from future_failed (no noise, no random label flips)
    df["at_risk"] = df["future_failed"].astype(int)

    # Keep only target columns
    cols_to_keep = ["EtudiantId", "period_key", "moyenne_generale", "taux_absence", "nb_modules", "at_risk"]
    df = df[cols_to_keep]

    # Verify we still have both classes
    if df["at_risk"].nunique() < 2:
        logger.warning("DW data has only one class after temporal lagging — falling back to synthetic.")
        return None

    logger.info("Loaded and processed %d student-periods from DW for risk training "
                "(%.1f%% at risk).", len(df), df["at_risk"].mean() * 100)
    return df


def _process_raw_forecast_data(df: pd.DataFrame) -> pd.DataFrame | None:
    """Processes aggregated student-period data to create features and future targets for regression model."""
    if len(df) < 30:
        logger.warning("DW has only %d student-periods — too few. "
                       "Falling back to synthetic data.", len(df))
        return None

    # Clip taux_absence to [0, 1]
    df["taux_absence"] = df["taux_absence"].clip(0, 1)

    # Convert Annee and Semestre to period_key
    df["period_key"] = df.apply(lambda row: _compute_period_key(str(row["Annee"]), str(row["Semestre"])), axis=1)

    # Sort by student and period
    df = df.sort_values(["EtudiantId", "period_key"])

    # Shift moyenne_generale backward by -1 per student (prior period predicts next-period moyenne)
    df["future_moyenne"] = df.groupby("EtudiantId")["moyenne_generale"].shift(-1)

    # Drop last period because it has no future label
    df = df.dropna(subset=["future_moyenne"]).copy()

    # Rename current moyenne to moyenne_actuelle, and future moyenne to note_finale
    df = df.rename(columns={
        "moyenne_generale": "moyenne_actuelle",
        "future_moyenne": "note_finale"
    })

    # Keep only target columns
    cols_to_keep = ["EtudiantId", "period_key", "moyenne_actuelle", "taux_absence", "nb_modules", "note_finale"]
    df = df[cols_to_keep]

    logger.info("Loaded and processed %d student-periods from DW for forecast training.", len(df))
    return df


def load_risk_data() -> pd.DataFrame | None:
    """
    Loads training data for the risk classifier from PFA_DW.
    """
    conn_str = _get_connection_string()
    if conn_str is None:
        return None

    try:
        import pyodbc

        query = f"""
            SELECT
                fn.EtudiantKey AS EtudiantId,
                dt.Annee,
                dt.Semestre,
                de.Matricule,
                AVG(fn.NoteFinale)                          AS moyenne_generale,
                SUM(fn.NbAbsences) * 1.0
                    / NULLIF(COUNT(DISTINCT fn.ModuleKey) * {SESSIONS_PER_MODULE}, 0) AS taux_absence,
                COUNT(DISTINCT fn.ModuleKey)                AS nb_modules
            FROM      FaitNotes   fn
            JOIN      DimEtudiant de ON de.EtudiantKey = fn.EtudiantKey
            JOIN      DimTemps    dt ON dt.TempsKey = fn.TempsKey
            WHERE     fn.NoteFinale IS NOT NULL
            GROUP BY  fn.EtudiantKey, de.Matricule, dt.Annee, dt.Semestre
            HAVING    COUNT(DISTINCT fn.ModuleKey) >= 1
        """

        with closing(pyodbc.connect(conn_str)) as conn:
            df = pd.read_sql(query, conn)

        return _process_raw_risk_data(df)

    except Exception as e:
        logger.warning("Could not load data from DW: %s — falling back to synthetic.", _safe_error(e))
        return None


def load_clustering_data() -> pd.DataFrame | None:
    """
    Loads training data for K-Means clustering from PFA_DW.
    """
    df = load_risk_data()
    if df is None:
        return None

    # Drop columns not used in clustering (label and identifiers)
    cols_to_drop = [c for c in ["at_risk", "EtudiantId", "period_key"] if c in df.columns]
    return df.drop(columns=cols_to_drop)


def load_forecast_data() -> pd.DataFrame | None:
    """
    Loads training data for grade forecasting from PFA_DW.
    """
    conn_str = _get_connection_string()
    if conn_str is None:
        return None

    try:
        import pyodbc

        query = f"""
            SELECT
                fn.EtudiantKey AS EtudiantId,
                dt.Annee,
                dt.Semestre,
                de.Matricule,
                AVG(fn.NoteFinale)                          AS moyenne_generale,
                SUM(fn.NbAbsences) * 1.0
                    / NULLIF(COUNT(DISTINCT fn.ModuleKey) * {SESSIONS_PER_MODULE}, 0) AS taux_absence,
                COUNT(DISTINCT fn.ModuleKey)                AS nb_modules
            FROM      FaitNotes   fn
            JOIN      DimEtudiant de ON de.EtudiantKey = fn.EtudiantKey
            JOIN      DimTemps    dt ON dt.TempsKey = fn.TempsKey
            WHERE     fn.NoteFinale IS NOT NULL
            GROUP BY  fn.EtudiantKey, de.Matricule, dt.Annee, dt.Semestre
            HAVING    COUNT(DISTINCT fn.ModuleKey) >= 1
        """

        with closing(pyodbc.connect(conn_str)) as conn:
            df = pd.read_sql(query, conn)

        return _process_raw_forecast_data(df)

    except Exception as e:
        logger.warning("Could not load forecast data from DW: %s — falling back to synthetic.", _safe_error(e))
        return None
