"""
Training script for the student failure-risk classifier.

Run once to produce saved_models/risk_model.joblib.
Re-run whenever you have new/real training data.

Usage:
    python models/train_risk.py
"""

import numpy as np
import pandas as pd
from pathlib import Path
from sklearn.model_selection import train_test_split
from sklearn.metrics import classification_report, roc_auc_score
from sklearn.preprocessing import StandardScaler
from sklearn.pipeline import Pipeline
import xgboost as xgb
import joblib

RANDOM_SEED = 42
N_SAMPLES = 1000
MODEL_PATH = Path(__file__).parent.parent / "saved_models" / "risk_model.joblib"


# ─── 1. Generate synthetic student data ───────────────────────────────────────

def generate_data(n: int, seed: int) -> pd.DataFrame:
    """
    Creates realistic fake student records.

    The label (at_risk) is derived from a simple rule:
      - High absence (> 0.30) OR very low average (< 8)  -> likely at risk
    We add gaussian noise so the model has to learn a fuzzy boundary,
    not a perfect rule -- which is closer to reality.
    """
    rng = np.random.default_rng(seed)

    moyenne = rng.uniform(0, 20, n)          # grade 0-20
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

def train(df: pd.DataFrame) -> Pipeline:
    """
    Wraps a StandardScaler + XGBoostClassifier in a sklearn Pipeline.

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
                                 target_names=["Not at risk", "At risk"]))
    print(f"ROC-AUC: {roc_auc_score(y_test, y_proba):.3f}")
    print("  (ROC-AUC of 1.0 = perfect, 0.5 = random -- aim for > 0.80)")

    return pipeline


# ─── 3. Save the trained pipeline to disk ─────────────────────────────────────

def save(pipeline: Pipeline) -> None:
    MODEL_PATH.parent.mkdir(parents=True, exist_ok=True)
    joblib.dump(pipeline, MODEL_PATH)
    print(f"\nModel saved -> {MODEL_PATH}")


# ─── Entry point ──────────────────────────────────────────────────────────────

if __name__ == "__main__":
    print("Generating synthetic training data...")
    df = generate_data(N_SAMPLES, RANDOM_SEED)
    print(f"  {len(df)} students | {df['at_risk'].mean():.1%} at risk\n")

    print("Training XGBoost classifier...")
    pipeline = train(df)

    save(pipeline)
