"""
Training script for the student failure-risk classifier.

Run once to produce saved_models/risk_model.joblib.
Re-run whenever you have new/real training data.

Usage:
    python models/train_risk.py
"""

import json
from datetime import datetime, timezone
from pathlib import Path

import numpy as np
import pandas as pd
from sklearn.model_selection import GroupShuffleSplit
from sklearn.metrics import (
    classification_report,
    roc_auc_score,
    f1_score,
    precision_score,
    recall_score,
)
from sklearn.preprocessing import StandardScaler
from sklearn.pipeline import Pipeline
import xgboost as xgb
import joblib

RANDOM_SEED = 42
N_SAMPLES = 1000
MODEL_VERSION = "1.7.0"

ML_MODELS_DIR = Path(__file__).parent.parent / "saved_models"
MODEL_PATH = ML_MODELS_DIR / "risk_model.joblib"


# ─── 1. Generate synthetic student data ───────────────────────────────────────

def generate_data(n: int, seed: int) -> pd.DataFrame:
    """
    Creates realistic fake student records over multiple periods.
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

        # Split students into two cohorts to simulate graduating / incoming students
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

    # Sort
    df = df.sort_values(["EtudiantId", "period_key"])

    # Define failed in current period
    df["failed"] = (df["moyenne_generale"] < 10.0).astype(int)

    # Shift failed backward by -1 per student (prior period predicts next-period failure)
    df["future_failed"] = df.groupby("EtudiantId")["failed"].shift(-1)

    # Drop last period because it has no future label
    df = df.dropna(subset=["future_failed"]).copy()

    df["at_risk"] = df["future_failed"].astype(int)
    df = df.drop(columns=["failed", "future_failed"])

    return df


# ─── 2. Build and train the model ─────────────────────────────────────────────

def train(df: pd.DataFrame) -> tuple[Pipeline, pd.DataFrame, pd.Series]:
    """
    Trains the classifier using out_of_time or grouped_student split.
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

    # Fallback to grouped_student if out_of_time leaves train_df empty
    if len(train_df) == 0 or len(test_df) == 0:
        gss = GroupShuffleSplit(n_splits=1, test_size=0.2, random_state=RANDOM_SEED)
        train_idx, test_idx = next(gss.split(df, groups=df["EtudiantId"]))
        train_df = df.iloc[train_idx].copy()
        test_df = df.iloc[test_idx].copy()
        split_strategy = "grouped_student"

    X_train = train_df[["moyenne_generale", "taux_absence", "nb_modules"]]
    y_train = train_df["at_risk"]
    X_test = test_df[["moyenne_generale", "taux_absence", "nb_modules"]]
    y_test = test_df["at_risk"]

    pipeline = Pipeline([
        ("scaler", StandardScaler()),
        ("classifier", xgb.XGBClassifier(
            n_estimators=60,
            max_depth=3,
            learning_rate=0.1,
            min_child_weight=8,
            subsample=0.8,
            colsample_bytree=0.8,
            reg_alpha=1.0,
            reg_lambda=2.0,
            eval_metric="logloss",
            random_state=RANDOM_SEED,
        )),
    ])

    pipeline.fit(X_train, y_train)

    # ── Evaluation ────────────────────────────────────────────
    y_pred = pipeline.predict(X_test)
    y_proba = pipeline.predict_proba(X_test)[:, 1]

    print(f"\n=== Risk Model Evaluation ({split_strategy}) ===")
    print(classification_report(y_test, y_pred,
                                 labels=[0, 1],
                                 target_names=["Not at risk", "At risk"],
                                 zero_division=0))
    try:
        auc_display = roc_auc_score(y_test, y_proba)
        print(f"ROC-AUC: {auc_display:.3f}")
    except ValueError:
        print("ROC-AUC: N/A (only one class present in test set)")

    pipeline.split_strategy = split_strategy
    pipeline.train_periods = list(train_df["period_key"].unique()) if "period_key" in train_df.columns else []
    pipeline.test_periods = list(test_df["period_key"].unique()) if "period_key" in test_df.columns else []
    pipeline.n_students_train = int(train_df["EtudiantId"].nunique()) if "EtudiantId" in train_df.columns else 0
    pipeline.n_students_test = int(test_df["EtudiantId"].nunique()) if "EtudiantId" in test_df.columns else 0

    return pipeline, X_test, y_test


# ─── 3. Save the trained pipeline + eval artefacts to disk ────────────────────

def save(pipeline: Pipeline) -> None:
    """Legacy single-arg save — keeps backward compatibility with auto_train."""
    MODEL_PATH.parent.mkdir(parents=True, exist_ok=True)
    joblib.dump(pipeline, MODEL_PATH)
    print(f"\nModel saved -> {MODEL_PATH}")


def save_with_metadata(
    pipeline: Pipeline,
    X_test: pd.DataFrame,
    y_test: pd.Series,
    models_dir: Path | None = None,
    data_source: str = "unknown",
) -> None:
    """
    Persists model and evaluation metrics metadata.
    """
    out_dir = models_dir or MODEL_PATH.parent
    out_dir.mkdir(parents=True, exist_ok=True)

    split_strategy = getattr(pipeline, "split_strategy", "unknown")

    # 3a. Joblib model
    model_file = out_dir / "risk_model.joblib"
    joblib.dump(pipeline, model_file)
    print(f"\nModel saved -> {model_file}")

    # 3b. Eval set parquet
    eval_df = X_test.copy()
    eval_df["y_true"] = y_test.values
    eval_path = out_dir / "eval_set.parquet"
    eval_df.to_parquet(eval_path, index=False)
    print(f"Eval set saved -> {eval_path}  ({len(eval_df)} rows)")

    # 3c. Compute metrics
    y_pred = pipeline.predict(X_test)
    y_proba = pipeline.predict_proba(X_test)[:, 1]

    try:
        auc = float(roc_auc_score(y_test, y_proba))
    except ValueError:
        auc = 0.0
    f1 = float(f1_score(y_test, y_pred, zero_division=0))
    precision = float(precision_score(y_test, y_pred, zero_division=0))
    recall = float(recall_score(y_test, y_pred, zero_division=0))

    metadata = {
        "model_version": MODEL_VERSION,
        "trained_at": datetime.now(timezone.utc).isoformat(),
        "auc": round(auc, 4),
        "f1": round(f1, 4),
        "precision": round(precision, 4),
        "recall": round(recall, 4),
        "n_samples": len(eval_df),
        "data_source": data_source,
        "split_strategy": split_strategy,
        "train_periods": [float(p) for p in getattr(pipeline, "train_periods", [])],
        "test_periods": [float(p) for p in getattr(pipeline, "test_periods", [])],
        "n_students_train": int(getattr(pipeline, "n_students_train", 0)),
        "n_students_test": int(getattr(pipeline, "n_students_test", 0))
    }

    meta_path = out_dir / "metadata.json"
    meta_path.write_text(json.dumps(metadata, indent=2), encoding="utf-8")
    print(f"Metadata saved -> {meta_path}")


# ─── Entry point ──────────────────────────────────────────────────────────────

if __name__ == "__main__":
    print("Generating synthetic training data...")
    df = generate_data(N_SAMPLES, RANDOM_SEED)
    print(f"  {len(df)} samples | {df['at_risk'].mean():.1%} at risk\n")

    print("Training XGBoost classifier...")
    pipeline, X_test, y_test = train(df)

    save_with_metadata(pipeline, X_test, y_test, data_source="synthetic")
