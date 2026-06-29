"""
Training script for the student failure-risk classifier.

Uses shared features from features/feature_engineering.py (5 features instead
of the original 3) and applies threshold optimisation to improve recall.

Threshold logic:
  - Finds the probability threshold that maximises F2-score (recall weighted
    2× vs precision) on the held-out test set.
  - Stores the chosen threshold in the pipeline metadata so the /predict
    endpoint can use it instead of the default 0.5.
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

from models.auto_train import SAVED_MODELS_DIR
from features import (
    FEATURE_COLS,
    generate_synthetic_risk_data,
    find_optimal_threshold,
    SEED,
    N_SAMPLES,
)

RANDOM_SEED = SEED
N_SAMPLES = 2000
MODEL_VERSION = "2.0.0"

MODEL_PATH = SAVED_MODELS_DIR / "risk_model.joblib"


def train(df: pd.DataFrame) -> tuple[Pipeline, pd.DataFrame, pd.Series, dict]:
    """
    Trains the classifier using out_of_time or grouped_student split.
    Returns (pipeline, X_test, y_test, threshold_info).
    """
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

    if len(train_df) == 0 or len(test_df) == 0:
        gss = GroupShuffleSplit(n_splits=1, test_size=0.2, random_state=RANDOM_SEED)
        train_idx, test_idx = next(gss.split(df, groups=df["EtudiantId"]))
        train_df = df.iloc[train_idx].copy()
        test_df = df.iloc[test_idx].copy()
        split_strategy = "grouped_student"

    X_train = train_df[FEATURE_COLS]
    y_train = train_df["at_risk"]
    X_test = test_df[FEATURE_COLS]
    y_test = test_df["at_risk"]

    pipeline = Pipeline([
        ("scaler", StandardScaler()),
        ("classifier", xgb.XGBClassifier(
            n_estimators=80,
            max_depth=4,
            learning_rate=0.08,
            min_child_weight=6,
            subsample=0.8,
            colsample_bytree=0.8,
            reg_alpha=1.0,
            reg_lambda=2.0,
            scale_pos_weight=(y_train == 0).sum() / max((y_train == 1).sum(), 1),
            eval_metric="logloss",
            random_state=RANDOM_SEED,
        )),
    ])

    pipeline.fit(X_train, y_train)

    # Default threshold evaluation (0.5)
    y_pred_default = pipeline.predict(X_test)
    y_proba = pipeline.predict_proba(X_test)[:, 1]

    print(f"\n=== Risk Model Evaluation ({split_strategy}) @ threshold=0.5 ===")
    print(classification_report(y_test, y_pred_default,
                                 labels=[0, 1],
                                 target_names=["Not at risk", "At risk"],
                                 zero_division=0))
    try:
        auc_display = roc_auc_score(y_test, y_proba)
        print(f"ROC-AUC: {auc_display:.3f}")
    except ValueError:
        print("ROC-AUC: N/A (only one class present in test set)")

    # ── Threshold optimization ──────────────────────────────
    threshold_info = find_optimal_threshold(
        y_test.values, y_proba, beta=2.0
    )
    chosen = threshold_info["chosen_threshold"]
    y_pred_opt = (y_proba >= chosen).astype(int)

    print(f"\n  Threshold optimization (F2, recall-weighted):")
    print(f"    Default (0.5)   -> recall={threshold_info['baseline_recall']:.3f}  precision={threshold_info['baseline_precision']:.3f}")
    print(f"    Optimal ({chosen:.3f}) -> recall={threshold_info['optimal_recall']:.3f}  precision={threshold_info['optimal_precision']:.3f}  F2={threshold_info['optimal_fbeta']:.3f}")
    print(f"    Youden's J ({threshold_info['youden_threshold']:.3f}) -> J={threshold_info['youden_j']:.3f}")

    pipeline.split_strategy = split_strategy
    pipeline.train_periods = list(train_df["period_key"].unique()) if "period_key" in train_df.columns else []
    pipeline.test_periods = list(test_df["period_key"].unique()) if "period_key" in test_df.columns else []
    pipeline.n_students_train = int(train_df["EtudiantId"].nunique()) if "EtudiantId" in train_df.columns else 0
    pipeline.n_students_test = int(test_df["EtudiantId"].nunique()) if "EtudiantId" in test_df.columns else 0
    pipeline.chosen_threshold = chosen

    return pipeline, X_test, y_test, threshold_info


def save_with_metadata(
    pipeline: Pipeline,
    X_test: pd.DataFrame,
    y_test: pd.Series,
    threshold_info: dict | None = None,
    models_dir: Path | None = None,
    data_source: str = "unknown",
) -> None:
    """
    Persists model, evaluation metrics, and threshold info to disk.
    """
    out_dir = models_dir or MODEL_PATH.parent
    out_dir.mkdir(parents=True, exist_ok=True)

    model_file = out_dir / "risk_model.joblib"
    joblib.dump(pipeline, model_file)
    print(f"\nModel saved -> {model_file}")

    eval_df = X_test.copy()
    eval_df["y_true"] = y_test.values
    eval_path = out_dir / "eval_set.parquet"
    eval_df.to_parquet(eval_path, index=False)
    print(f"Eval set saved -> {eval_path}  ({len(eval_df)} rows)")

    y_pred = pipeline.predict(X_test)
    y_proba = pipeline.predict_proba(X_test)[:, 1]
    chosen_t = threshold_info["chosen_threshold"] if threshold_info else 0.5
    y_pred_opt = (y_proba >= chosen_t).astype(int)

    try:
        auc = float(roc_auc_score(y_test, y_proba))
    except ValueError:
        auc = 0.0

    metadata = {
        "model_version": MODEL_VERSION,
        "trained_at": datetime.now(timezone.utc).isoformat(),
        "auc": round(auc, 4),
        "f1": float(f1_score(y_test, y_pred, zero_division=0)),
        "f1_optimal": float(f1_score(y_test, y_pred_opt, zero_division=0)),
        "precision": float(precision_score(y_test, y_pred, zero_division=0)),
        "recall": float(recall_score(y_test, y_pred, zero_division=0)),
        "recall_optimal": float(recall_score(y_test, y_pred_opt, zero_division=0)),
        "n_samples": len(eval_df),
        "data_source": data_source,
        "split_strategy": getattr(pipeline, "split_strategy", "unknown"),
        "n_features": len(FEATURE_COLS),
        "features": list(FEATURE_COLS),
        "chosen_threshold": float(chosen_t),
        "threshold_info": threshold_info or {},
        "train_periods": [float(p) for p in getattr(pipeline, "train_periods", [])],
        "test_periods": [float(p) for p in getattr(pipeline, "test_periods", [])],
        "n_students_train": int(getattr(pipeline, "n_students_train", 0)),
        "n_students_test": int(getattr(pipeline, "n_students_test", 0)),
    }

    meta_path = out_dir / "metadata.json"
    meta_path.write_text(json.dumps(metadata, indent=2), encoding="utf-8")
    print(f"Metadata saved -> {meta_path}")

    # MLflow tracking (best-effort)
    from mlflow_client import start_run, log_params, log_metrics, log_tags, log_artifact

    clf = pipeline.named_steps.get("classifier")
    clf_params = clf.get_params() if clf is not None else {}

    with start_run("risk_model"):
        log_params({
            "model_version": MODEL_VERSION,
            "data_source": data_source,
            "split_strategy": getattr(pipeline, "split_strategy", "unknown"),
            "n_samples": len(eval_df),
            "n_students_train": int(getattr(pipeline, "n_students_train", 0)),
            "n_features": len(FEATURE_COLS),
            "chosen_threshold": float(chosen_t),
            "n_estimators": clf_params.get("n_estimators"),
            "max_depth": clf_params.get("max_depth"),
            "learning_rate": clf_params.get("learning_rate"),
            "random_state": clf_params.get("random_state"),
            "scale_pos_weight": clf_params.get("scale_pos_weight"),
        })
        log_metrics({
            "auc": auc,
            "f1": float(f1_score(y_test, y_pred, zero_division=0)),
            "recall_optimal": float(recall_score(y_test, y_pred_opt, zero_division=0)),
            "recall_baseline": float(recall_score(y_test, y_pred, zero_division=0)),
        })
        log_tags({
            "model_name": "risk_model",
            "features": ",".join(FEATURE_COLS),
            "train_periods": ",".join(str(p) for p in getattr(pipeline, "train_periods", [])),
            "test_periods": ",".join(str(p) for p in getattr(pipeline, "test_periods", [])),
        })
        log_artifact(model_file)
        log_artifact(eval_path)
        log_artifact(meta_path)


if __name__ == "__main__":
    print("Generating synthetic training data...")
    df = generate_synthetic_risk_data(N_SAMPLES, RANDOM_SEED)
    print(f"  {len(df)} samples | {df['at_risk'].mean():.1%} at risk | {len(FEATURE_COLS)} features\n")

    print("Training XGBoost classifier...")
    pipeline, X_test, y_test, threshold_info = train(df)

    save_with_metadata(pipeline, X_test, y_test, threshold_info=threshold_info, data_source="synthetic")
