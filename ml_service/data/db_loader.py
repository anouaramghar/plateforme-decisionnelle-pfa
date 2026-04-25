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
import logging

import pandas as pd

logger = logging.getLogger(__name__)


def _get_connection_string() -> str | None:
    """Returns the ODBC connection string or None if not configured."""
    conn_str = os.getenv("DW_CONNECTION_STRING")
    if not conn_str:
        logger.info("DW_CONNECTION_STRING not set — will use synthetic data.")
        return None
    return conn_str


def load_risk_data() -> pd.DataFrame | None:
    """
    Loads training data for the risk classifier from PFA_DW.

    Aggregates per student:
      - moyenne_generale: AVG(NoteFinale) across all modules
      - taux_absence:     total NbAbsences / estimated scheduled hours
      - nb_modules:       COUNT of distinct modules
      - at_risk:          1 if moyenne < 10, else 0 (label)

    Returns None if DW is unreachable or has insufficient data (< 30 rows).
    """
    conn_str = _get_connection_string()
    if conn_str is None:
        return None

    try:
        import pyodbc
        conn = pyodbc.connect(conn_str)

        query = """
            SELECT
                de.Matricule,
                AVG(fn.NoteFinale)                          AS moyenne_generale,
                SUM(fn.NbAbsences) * 1.0
                    / NULLIF(COUNT(DISTINCT fn.ModuleKey) * 32, 0) AS taux_absence,
                COUNT(DISTINCT fn.ModuleKey)                AS nb_modules
            FROM      FaitNotes   fn
            JOIN      DimEtudiant de ON de.EtudiantKey = fn.EtudiantKey
            WHERE     fn.NoteFinale IS NOT NULL
            GROUP BY  de.EtudiantKey, de.Matricule
            HAVING    COUNT(DISTINCT fn.ModuleKey) >= 1
        """

        df = pd.read_sql(query, conn)
        conn.close()

        if len(df) < 30:
            logger.warning("DW has only %d students — too few for reliable training. "
                           "Falling back to synthetic data.", len(df))
            return None

        # Clip taux_absence to [0, 1]
        df["taux_absence"] = df["taux_absence"].clip(0, 1)

        # Label: at_risk = 1 if average grade < 10
        df["at_risk"] = (df["moyenne_generale"] < 10).astype(int)

        # Drop Matricule (not a feature)
        df = df.drop(columns=["Matricule"])

        logger.info("Loaded %d students from DW for risk training "
                     "(%.1f%% at risk).", len(df), df["at_risk"].mean() * 100)
        return df

    except Exception as e:
        logger.warning("Could not load data from DW: %s — falling back to synthetic.", e)
        return None


def load_clustering_data() -> pd.DataFrame | None:
    """
    Loads training data for K-Means clustering from PFA_DW.

    Same aggregation as risk but without the label (unsupervised).
    Returns: DataFrame with [moyenne_generale, taux_absence, nb_modules]
    """
    df = load_risk_data()
    if df is None:
        return None

    # Drop the label column — clustering is unsupervised
    return df.drop(columns=["at_risk"])


def load_forecast_data() -> pd.DataFrame | None:
    """
    Loads training data for grade forecasting from PFA_DW.

    For each (student, module, semester), returns:
      - moyenne_actuelle: the student's overall average at that point
      - taux_absence:     absence rate for that module
      - nb_modules:       how many modules the student is enrolled in
      - note_finale:      the actual final grade (regression target)

    Returns None if DW is unreachable or has insufficient data.
    """
    conn_str = _get_connection_string()
    if conn_str is None:
        return None

    try:
        import pyodbc
        conn = pyodbc.connect(conn_str)

        # Window functions instead of two correlated subqueries per row.
        # Old plan was O(N²) — for ~7 800 rows that meant ~15 600 inner scans of
        # FaitNotes. Window AVG/COUNT runs once per partition.
        query = """
            SELECT
                fn.NoteFinale                                                 AS note_finale,
                fn.NbAbsences * 1.0 / 32.0                                    AS taux_absence,
                AVG(CAST(fn.NoteFinale AS FLOAT))     OVER (PARTITION BY fn.EtudiantKey) AS moyenne_actuelle,
                COUNT(DISTINCT fn.ModuleKey)          OVER (PARTITION BY fn.EtudiantKey) AS nb_modules
            FROM FaitNotes fn
            WHERE fn.NoteFinale IS NOT NULL
        """

        df = pd.read_sql(query, conn)
        conn.close()

        if len(df) < 30:
            logger.warning("DW has only %d grade records — too few. "
                           "Falling back to synthetic data.", len(df))
            return None

        df["taux_absence"] = df["taux_absence"].clip(0, 1)
        df = df.dropna()

        logger.info("Loaded %d grade records from DW for forecast training.", len(df))
        return df

    except Exception as e:
        logger.warning("Could not load forecast data from DW: %s — falling back to synthetic.", e)
        return None
