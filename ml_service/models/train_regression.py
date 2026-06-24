"""
Training script for grade forecasting (XGBoost regression).

This predicts a student's FINAL average grade (note_finale) as a continuous
number given their mid-semester average, absence rate, and module count.

Usage:
    python models/train_regression.py
"""

import json
from datetime import datetime, timezone
from pathlib import Path

import numpy as np
import pandas as pd
from sklearn.model_selection import GroupShuffleSplit
from sklearn.metrics import mean_absolute_error, r2_score
from sklearn.preprocessing import StandardScaler
from sklearn.pipeline import Pipeline
import xgboost as xgb
import joblib

from models.auto_train import SAVED_MODELS_DIR

RANDOM_SEED = 42
N_SAMPLES = 1000
MODEL_VERSION = "1.8.0"
MODEL_PATH = SAVED_MODELS_DIR / "forecast_model.joblib"


# ─── 1. Generate synthetic student data ───────────────────────────────────────

def generate_data(n: int, seed: int) -> pd.DataFrame:
    """
    Creates realistic student records over multiple periods.
    Disjoint student groups are generated for different periods to avoid
    total removal during student isolation in train/test splits.
    """
    rng = np.random.default_rng(seed)

    n_students = max(n, 20)

    student_ids = []
    period_keys = []
    moyennes = []
    absences = []
    nb_modules_list = []

    for i in range(n_students):
        student_base_grade = rng.uniform(6, 17)
        student_base_absence = rng.uniform(0.01, 0.4)
        nb_mods = int(rng.integers(3, 12))

        if i < n_students // 2:
            periods = [2025.1, 2025.2]
        else:
            periods = [2025.2, 2026.1]

        for p in periods:
            student_ids.append(i + 1)
            period_keys.append(p)

            grade = np.clip(student_base_grade + rng.normal(0, 1.5), 0, 20)
            absence = np.clip(student_base_absence + rng.normal(0, 0.05), 0, 1)

            moyennes.append(grade)
            absences.append(absence)
            nb_modules_list.append(nb_mods)

    df = pd.DataFrame({
        "EtudiantId": student_ids,
        "period_key": period_keys,
        "moyenne_generale": moyennes,
        "taux_absence": absences,
        "nb_modules": nb_modules_list
    })

    df = df.sort_values(["EtudiantId", "period_key"])

    df["future_moyenne"] = df.groupby("EtudiantId")["moyenne_generale"].shift(-1)
    df = df.dropna(subset=["future_moyenne"]).copy()

    df = df.rename(columns={
        "moyenne_generale": "moyenne_actuelle",
        "future_moyenne": "note_finale"
    })

    return df


# ─── 2. Build and train the regression model ──────────────────────────────────

def train(df: pd.DataFrame) -> Pipeline:
    """
    Trains the XGBRegressor using out_of_time or grouped_student split.
    """
    # Time/student split logic
    unique_periods = df["period_key"].nunique()
    if unique_periods > 1:
        max_period = df["period_key"].max()
        test_df = df[df.period_key == max_period].copy()
        train_df = df[df.period_key < max_period].copy()

        train_df = train_df[~train_df.EtudiantId.isin(test_df.EtudiantId)].copy()
        split_strategy = "out_of_time"
    else:
        gss = GroupShuffleSplit(n_splits=1, test_size=0.2, random_state=RANDOM_SEED)
        train_idx, test_idx = next(gss.split(df, groups=df["EtudiantId"]))
        train_df = df.iloc[train_idx].copy()
        test_df = df.iloc[test_idx].copy()
        split_strategy = "grouped_student"

    # Fallback if out_of_time leaves train_df empty
    if len(train_df) == 0 or len(test_df) == 0:
        gss = GroupShuffleSplit(n_splits=1, test_size=0.2, random_state=RANDOM_SEED)
        train_idx, test_idx = next(gss.split(df, groups=df["EtudiantId"]))
        train_df = df.iloc[train_idx].copy()
        test_df = df.iloc[test_idx].copy()
        split_strategy = "grouped_student"

    X_train = train_df[["moyenne_actuelle", "taux_absence", "nb_modules"]]
    y_train = train_df["note_finale"]
    X_test = test_df[["moyenne_actuelle", "taux_absence", "nb_modules"]]
    y_test = test_df["note_finale"]

    pipeline = Pipeline([
        ("scaler", StandardScaler()),
        ("regressor", xgb.XGBRegressor(
            n_estimators=100,
            max_depth=4,
            learning_rate=0.1,
            eval_metric="rmse",
            random_state=RANDOM_SEED,
        )),
    ])

    pipeline.fit(X_train, y_train)

    # ── Evaluation ────────────────────────────────────────────
    y_pred = pipeline.predict(X_test)
    y_pred_clipped = np.clip(y_pred, 0, 20)

    mae = mean_absolute_error(y_test, y_pred_clipped)
    r2 = r2_score(y_test, y_pred_clipped)

    print(f"\n=== Forecast Model Evaluation ({split_strategy}) ===")
    print(f"MAE  : {mae:.2f} grade points  (lower is better)")
    print(f"R²   : {r2:.3f}               (closer to 1.0 is better)")

    pipeline.split_strategy = split_strategy
    pipeline.train_periods = list(train_df["period_key"].unique()) if "period_key" in train_df.columns else []
    pipeline.test_periods = list(test_df["period_key"].unique()) if "period_key" in test_df.columns else []
    pipeline.n_students_train = int(train_df["EtudiantId"].nunique()) if "EtudiantId" in train_df.columns else 0
    pipeline.n_students_test = int(test_df["EtudiantId"].nunique()) if "EtudiantId" in test_df.columns else 0

    return pipeline, X_test, y_test


# ─── 3. Save the trained pipeline to disk ─────────────────────────────────────

def save(pipeline: Pipeline, X_test: pd.DataFrame | None, y_test: pd.Series | None) -> None:
    MODEL_PATH.parent.mkdir(parents=True, exist_ok=True)
    save_with_metadata(pipeline, X_test, y_test, data_source="synthetic")


def save_with_metadata(
    pipeline: Pipeline,
    X_test: pd.DataFrame | None,
    y_test: pd.Series | None,
    models_dir: Path | None = None,
    data_source: str = "unknown"
) -> None:
    out_dir = models_dir or MODEL_PATH.parent
    out_dir.mkdir(parents=True, exist_ok=True)

    if X_test is not None and y_test is not None:
        eval_df = X_test.copy()
        eval_df["y_true"] = y_test.values
        eval_path = out_dir / "forecast_eval_set.parquet"
        eval_df.to_parquet(eval_path, index=False)
        print(f"Eval set saved -> {eval_path}  ({len(eval_df)} rows)")

        y_pred = pipeline.predict(X_test)
        y_pred_clipped = np.clip(y_pred, 0, 20)
        mae = float(mean_absolute_error(y_test, y_pred_clipped))
        r2 = float(r2_score(y_test, y_pred_clipped))
        n_samples = len(eval_df)
    else:
        mae = 0.0
        r2 = 0.0
        n_samples = 0

    model_file = out_dir / "forecast_model.joblib"
    joblib.dump(pipeline, model_file)
    print(f"\nModel saved -> {model_file}")

    metadata = {
        "model_version": MODEL_VERSION,
        "trained_at": datetime.now(timezone.utc).isoformat(),
        "mae": round(mae, 4),
        "r2": round(r2, 4),
        "n_samples": n_samples,
        "data_source": data_source,
        "split_strategy": getattr(pipeline, "split_strategy", "unknown"),
        "train_periods": [float(p) for p in getattr(pipeline, "train_periods", [])],
        "test_periods": [float(p) for p in getattr(pipeline, "test_periods", [])],
        "n_students_train": int(getattr(pipeline, "n_students_train", 0)),
        "n_students_test": int(getattr(pipeline, "n_students_test", 0))
    }

    meta_path = out_dir / "forecast_metadata.json"
    meta_path.write_text(json.dumps(metadata, indent=2), encoding="utf-8")
    print(f"Metadata saved -> {meta_path}")

    # ── MLflow tracking (best-effort) ──────────────────────────
    from mlflow_client import start_run, log_params, log_metrics, log_tags, log_artifact

    reg = pipeline.named_steps.get("regressor")
    reg_params = reg.get_params() if reg is not None else {}

    with start_run("forecast_model"):
        log_params({
            "model_version": MODEL_VERSION,
            "data_source": data_source,
            "split_strategy": getattr(pipeline, "split_strategy", "unknown"),
            "n_samples": n_samples,
            "n_students_train": int(getattr(pipeline, "n_students_train", 0)),
            "n_students_test": int(getattr(pipeline, "n_students_test", 0)),
            "n_estimators": reg_params.get("n_estimators"),
            "max_depth": reg_params.get("max_depth"),
            "learning_rate": reg_params.get("learning_rate"),
        })
        log_metrics({"mae": mae, "r2": r2})
        log_tags({
            "model_name": "forecast_model",
            "train_periods": ",".join(str(p) for p in getattr(pipeline, "train_periods", [])),
            "test_periods": ",".join(str(p) for p in getattr(pipeline, "test_periods", [])),
        })
        log_artifact(model_file)
        if X_test is not None and y_test is not None:
            log_artifact(out_dir / "forecast_eval_set.parquet")
        log_artifact(meta_path)


# ─── Entry point ──────────────────────────────────────────────────────────────

if __name__ == "__main__":
    print("Generating synthetic student data...")
    df = generate_data(N_SAMPLES, RANDOM_SEED)
    print(f"  {len(df)} samples | avg final grade: {df['note_finale'].mean():.1f}/20\n")

    print("Training XGBoost regressor...")
    pipeline, X_test, y_test = train(df)

    save_with_metadata(pipeline, X_test, y_test, data_source="synthetic")
