"""
Training script for the student failure-risk classifier.

Run once to produce saved_models/risk_model.joblib.
Re-run whenever you have new/real training data.

Usage:
    python models/train_risk.py
"""

import json
from datetime import datetime, timezone

import numpy as np
import pandas as pd
from pathlib import Path
from sklearn.model_selection import train_test_split
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
MODEL_VERSION = "1.4.0"

ML_MODELS_DIR = Path(__file__).parent.parent / "saved_models"
MODEL_PATH = ML_MODELS_DIR / "risk_model.joblib"


# ─── 1. Generate synthetic student data ───────────────────────────────────────

def generate_data(n: int, seed: int) -> pd.DataFrame:
    """
    Creates realistic fake student records.

    The label (at_risk) is derived from a simple rule:
      - High absence (> 0.30) OR very low average (< 8)  -> likely at risk
    We add gaussian noise so the model has to learn a fuzzy boundary,
    not a perfect rule -- which is closer to reality.

    Moyenne uses a truncated normal (mean=11.5, std=3.5) to approximate
    real grade distributions that cluster around 10-14, rather than a
    uniform distribution which would over-represent extremes.
    """
    rng = np.random.default_rng(seed)

    # Truncated normal: real grades cluster around 10-14, not uniform 0-20.
    moyenne = np.clip(rng.normal(loc=11.5, scale=3.5, size=n), 0, 20)
    absence = rng.uniform(0, 0.6, n)         # absence rate 0-60%
    nb_modules = rng.integers(3, 12, n)      # 3 to 11 modules

    # Base risk score: low grade and high absence both increase risk
    risk_score = (
        (20 - moyenne) / 20 * 0.6   +   # grade contributes 60%
        absence * 0.4                    # absence contributes 40%
    )
    # Add noise so it's not a perfect deterministic rule
    risk_score += rng.normal(0, 0.08, n)
    risk_score = np.clip(risk_score, 0, 1)

    # Label: 1 = at risk, 0 = not at risk (threshold 0.45)
    at_risk = (risk_score > 0.45).astype(int)

    return pd.DataFrame({
        "moyenne_generale": moyenne,
        "taux_absence": absence,
        "nb_modules": nb_modules,
        "at_risk": at_risk,
    })


# ─── 2. Build and train the model ─────────────────────────────────────────────

def train(df: pd.DataFrame) -> tuple[Pipeline, pd.DataFrame, pd.Series]:
    """
    Wraps a StandardScaler + XGBoostClassifier in a sklearn Pipeline.

    Returns:
        (pipeline, X_test, y_test) — the held-out evaluation set is returned
        so callers can persist it for on-the-fly metric recomputation.

    Why a Pipeline?
      A Pipeline chains preprocessing + model into one object.
      When you call pipeline.predict(X), it automatically scales first.
      This means we save ONE file (the pipeline) and it handles everything.

    Why StandardScaler?
      XGBoost does not strictly need it, but it makes the model more robust
      when features have very different scales (grades 0-20 vs absence 0-1).
    """
    X = df[["moyenne_generale", "taux_absence", "nb_modules"]]
    y = df["at_risk"]

    X_train, X_test, y_train, y_test = train_test_split(
        X, y, test_size=0.2, random_state=RANDOM_SEED, stratify=y
    )

    pipeline = Pipeline([
        ("scaler", StandardScaler()),
        ("classifier", xgb.XGBClassifier(
            n_estimators=100,       # 100 decision trees
            max_depth=4,            # each tree can be 4 levels deep
            learning_rate=0.1,      # how much each tree corrects the previous
            eval_metric="logloss",
            random_state=RANDOM_SEED,
        )),
    ])

    pipeline.fit(X_train, y_train)

    # ── Evaluation ────────────────────────────────────────────
    y_pred = pipeline.predict(X_test)
    y_proba = pipeline.predict_proba(X_test)[:, 1]

    print("\n=== Risk Model Evaluation ===")
    print(classification_report(y_test, y_pred,
                                 labels=[0, 1],
                                 target_names=["Not at risk", "At risk"],
                                 zero_division=0))
    try:
        auc_display = roc_auc_score(y_test, y_proba)
        print(f"ROC-AUC: {auc_display:.3f}")
        print("  (ROC-AUC of 1.0 = perfect, 0.5 = random -- aim for > 0.80)")
    except ValueError:
        print("ROC-AUC: N/A (only one class present in test set)")

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
    Persists:
      - risk_model.joblib  — the trained pipeline
      - eval_set.parquet   — X_test with y_true column (for /metrics recompute)
      - metadata.json      — snapshot of AUC/F1/precision/recall/n_samples
                             plus data_source so the dashboard can warn when
                             metrics were computed against synthetic data.

    Args:
        pipeline:    Trained sklearn Pipeline.
        X_test:      Held-out feature DataFrame.
        y_test:      Held-out labels Series.
        models_dir:  Override the output directory (used by staging retrain).
                     Defaults to ML_MODELS_DIR (saved_models/ next to models/).
        data_source: Provenance tag — one of "synthetic" | "dw" | "unknown".
                     The held-out 200-row "test" set is still i.i.d. from the
                     synthetic generator when data_source == "synthetic", so
                     the reported AUC reflects fit-to-generator, not real
                     generalisation. Surface this to consumers.
    """
    out_dir = models_dir or MODEL_PATH.parent
    out_dir.mkdir(parents=True, exist_ok=True)

    # 3a. Joblib model
    model_file = out_dir / "risk_model.joblib"
    joblib.dump(pipeline, model_file)
    print(f"\nModel saved -> {model_file}")

    # 3b. Eval set parquet  (X features + y_true column)
    eval_df = X_test.copy()
    eval_df["y_true"] = y_test.values
    eval_path = out_dir / "eval_set.parquet"
    eval_df.to_parquet(eval_path, index=False)
    print(f"Eval set saved -> {eval_path}  ({len(eval_df)} rows)")

    # 3c. Compute metrics and write metadata.json
    y_pred = pipeline.predict(X_test)
    y_proba = pipeline.predict_proba(X_test)[:, 1]

    try:
        auc = float(roc_auc_score(y_test, y_proba))
    except ValueError:
        auc = 0.0  # only one class in test set — not meaningful
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
    }

    meta_path = out_dir / "metadata.json"
    meta_path.write_text(json.dumps(metadata, indent=2), encoding="utf-8")
    print(f"Metadata saved -> {meta_path}")


# ─── Entry point ──────────────────────────────────────────────────────────────

if __name__ == "__main__":
    print("Generating synthetic training data...")
    df = generate_data(N_SAMPLES, RANDOM_SEED)
    print(f"  {len(df)} students | {df['at_risk'].mean():.1%} at risk\n")

    print("Training XGBoost classifier...")
    pipeline, X_test, y_test = train(df)

    save_with_metadata(pipeline, X_test, y_test)
