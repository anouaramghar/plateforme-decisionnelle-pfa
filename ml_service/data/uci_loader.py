"""
Loads the UCI Student Performance dataset and maps it to our 5-feature schema.

Dataset:
  - Student Performance Data Set (UCI ML Repository)
  - https://archive.ics.uci.edu/dataset/320/student+performance
  - 1044 records (math + Portuguese), 33 features
  - G1, G2, G3 grades (0-20), absences (0-93), failures (0-4)

Feature mapping:
  moyenne_generale       <- G2 (current grade)
  taux_absence           <- absences / 93.0 (normalized)
  nb_modules             <- 7 (constant - UCI is single-subject per row)
  ecart_type_modules     <- |G1 - G2| (grade variability, sample std dev)
  nb_echecs_anterieurs   <- failures

Targets:
  at_risk      <- G3 < 10 (risk model)
  note_finale  <- G3 (forecast model)

Limitation: UCI records are single-period snapshots (no temporal lag).
Grouped student split is used instead of out_of_time split.
"""

import logging
import zipfile
from pathlib import Path

import numpy as np
import pandas as pd

from features import FEATURE_COLS

logger = logging.getLogger(__name__)

UCI_ZIP_URL = "https://archive.ics.uci.edu/static/public/320/student+performance.zip"
CSV_FILES = ["student-mat.csv", "student-por.csv"]

DATA_DIR = Path(__file__).parent / "raw"


def _download_zip() -> Path | None:
    zip_path = DATA_DIR / "student+performance.zip"
    if zip_path.exists():
        logger.info("Using cached zip: %s", zip_path)
        return zip_path
    try:
        import requests
        DATA_DIR.mkdir(parents=True, exist_ok=True)
        logger.info("Downloading UCI dataset from %s ...", UCI_ZIP_URL)
        resp = requests.get(UCI_ZIP_URL, timeout=60)
        resp.raise_for_status()
        zip_path.write_bytes(resp.content)
        logger.info("Downloaded %s (%d bytes)", zip_path, len(resp.content))
        return zip_path
    except Exception as e:
        logger.warning("Could not download UCI dataset: %s", e)
        return None


def _read_csvs_from_zip(zip_path: Path) -> list[pd.DataFrame]:
    import io as _io
    dfs = []
    with zipfile.ZipFile(zip_path) as z:
        # UCI repo wraps CSVs inside an inner student.zip
        inner_name = "student.zip"
        if inner_name in z.namelist():
            with z.open(inner_name) as inner_bytes:
                with zipfile.ZipFile(_io.BytesIO(inner_bytes.read())) as inner:
                    for fname in CSV_FILES:
                        if fname not in inner.namelist():
                            logger.warning("Inner zip missing %s, skipping", fname)
                            continue
                        with inner.open(fname) as f:
                            df = pd.read_csv(f, sep=";")
                            logger.info("Extracted %s (%d rows)", fname, len(df))
                            dfs.append(df)
        else:
            for fname in CSV_FILES:
                if fname not in z.namelist():
                    logger.warning("Zip missing %s, skipping", fname)
                    continue
                with z.open(fname) as f:
                    df = pd.read_csv(f, sep=";")
                    logger.info("Extracted %s (%d rows)", fname, len(df))
                    dfs.append(df)
    return dfs


def _load_csvs() -> list[pd.DataFrame] | None:
    cached = []
    for fname in CSV_FILES:
        path = DATA_DIR / fname
        if path.exists():
            df = pd.read_csv(path, sep=";")
            logger.info("Loaded local %s (%d rows)", fname, len(df))
            cached.append(df)
    if len(cached) == len(CSV_FILES):
        return cached

    zip_path = _download_zip()
    if zip_path is None:
        if cached:
            return cached
        return None

    extracted = _read_csvs_from_zip(zip_path)
    if not extracted:
        if cached:
            return cached
        return None

    DATA_DIR.mkdir(parents=True, exist_ok=True)
    for df, fname in zip(extracted, CSV_FILES):
        df.to_csv(DATA_DIR / fname, sep=";", index=False)
    return extracted


def _map_features(df: pd.DataFrame) -> pd.DataFrame:
    g1 = pd.to_numeric(df["G1"], errors="coerce")
    g2 = pd.to_numeric(df["G2"], errors="coerce")
    g3 = pd.to_numeric(df["G3"], errors="coerce")
    absences = pd.to_numeric(df["absences"], errors="coerce")
    failures = pd.to_numeric(df["failures"], errors="coerce")

    result = pd.DataFrame()
    result["moyenne_generale"] = g2.clip(0, 20)
    result["taux_absence"] = (absences / 93.0).clip(0, 1)
    result["nb_modules"] = 7
    result["ecart_type_modules"] = (g1 - g2).abs().clip(0, 20)
    result["nb_echecs_anterieurs"] = failures.clip(0, 5).astype(int)

    result["note_finale"] = g3.clip(0, 20)
    result["at_risk"] = (g3 < 10).astype(int)
    return result


def load_data() -> pd.DataFrame | None:
    csvs = _load_csvs()
    if not csvs:
        logger.warning("Could not load UCI dataset from any source.")
        return None

    dfs = [_map_features(df) for df in csvs]
    combined = pd.concat(dfs, ignore_index=True)
    combined = combined.dropna(subset=["moyenne_generale", "note_finale"])

    combined["EtudiantId"] = np.arange(len(combined))
    combined["period_key"] = 1.0

    logger.info(
        "UCI dataset: %d students, %.1f%% at risk (G3<10), %.1f avg grade",
        len(combined),
        combined["at_risk"].mean() * 100,
        combined["note_finale"].mean(),
    )
    return combined


def load_uci_risk_data() -> pd.DataFrame | None:
    df = load_data()
    if df is None:
        return None
    cols = ["EtudiantId", "period_key"] + FEATURE_COLS + ["at_risk"]
    return df[cols]


def load_uci_forecast_data() -> pd.DataFrame | None:
    df = load_data()
    if df is None:
        return None
    forecast_cols = [
        "moyenne_actuelle", "taux_absence", "nb_modules",
        "ecart_type_modules", "nb_echecs_anterieurs",
    ]
    result = df.rename(columns={"moyenne_generale": "moyenne_actuelle"})
    cols = ["EtudiantId", "period_key"] + forecast_cols + ["note_finale"]
    return result[cols]


def load_uci_clustering_data() -> pd.DataFrame | None:
    df = load_data()
    if df is None:
        return None
    return df[FEATURE_COLS]
