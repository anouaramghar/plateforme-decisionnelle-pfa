"""
Training script for student segmentation (K-Means clustering).

Uses shared features (5 instead of 3) to produce 4 student profiles.
Replaces the earlier 3-feature version with richer feature space.
"""

import json
from datetime import datetime, timezone
import numpy as np
import pandas as pd
from pathlib import Path
from sklearn.preprocessing import StandardScaler
from sklearn.cluster import KMeans
from sklearn.pipeline import Pipeline
import joblib

from models.auto_train import SAVED_MODELS_DIR
from features import (
    FEATURE_COLS,
    generate_synthetic_cluster_data,
    SEED,
    N_SAMPLES,
)

RANDOM_SEED = SEED
K = 4
MODEL_VERSION = "2.0.0"
MODEL_PATH = SAVED_MODELS_DIR / "cluster_model.joblib"


def elbow_method(df: pd.DataFrame) -> None:
    """
    Trains K-Means for K=2..10 and saves an elbow plot to disk.
    """
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    scaler = StandardScaler()
    X_scaled = scaler.fit_transform(df[FEATURE_COLS])

    inertias = []
    k_range = range(2, 11)
    for k in k_range:
        km = KMeans(n_clusters=k, random_state=RANDOM_SEED, n_init=10)
        km.fit(X_scaled)
        inertias.append(km.inertia_)

    plt.figure(figsize=(8, 4))
    plt.plot(list(k_range), inertias, marker="o")
    plt.xlabel("Number of clusters K")
    plt.ylabel("Inertia (sum of squared distances)")
    plt.title("Elbow Method — choose K where the curve bends")
    plt.tight_layout()
    out = SAVED_MODELS_DIR / "elbow_plot.png"
    plt.savefig(out)
    print(f"Elbow plot saved -> {out}")


def train(df: pd.DataFrame, k: int) -> Pipeline:
    """
    Wraps StandardScaler + KMeans in a Pipeline.
    """
    X = df[FEATURE_COLS]

    pipeline = Pipeline([
        ("scaler", StandardScaler()),
        ("kmeans", KMeans(
            n_clusters=k,
            random_state=RANDOM_SEED,
            n_init=10,
            max_iter=300,
        )),
    ])

    pipeline.fit(X)

    labels = pipeline.named_steps["kmeans"].labels_
    df_result = df.copy()
    df_result["cluster"] = labels

    print(f"\n=== Cluster Profiles (K={k}, {len(FEATURE_COLS)} features) ===")
    agg_cols = {col: "mean" for col in FEATURE_COLS}
    agg_cols["cluster"] = "size"
    profile = df_result.groupby("cluster").agg(agg_cols).round(2)
    profile = profile.rename(columns={"cluster": "count"})
    print(profile.to_string())

    return pipeline


def save_with_metadata(
    pipeline: Pipeline,
    k: int,
    n_samples: int,
    models_dir: Path | None = None,
) -> None:
    out_dir = models_dir or MODEL_PATH.parent
    out_dir.mkdir(parents=True, exist_ok=True)

    model_file = out_dir / "cluster_model.joblib"
    joblib.dump(pipeline, model_file)
    print(f"\nModel saved -> {model_file}")

    metadata = {
        "model_version": MODEL_VERSION,
        "k": k,
        "n_samples": n_samples,
        "n_features": len(FEATURE_COLS),
        "features": list(FEATURE_COLS),
        "trained_at": datetime.now(timezone.utc).isoformat(),
    }
    meta_path = out_dir / "cluster_metadata.json"
    meta_path.write_text(json.dumps(metadata, indent=2), encoding="utf-8")
    print(f"Metadata saved -> {meta_path}")

    from mlflow_client import start_run, log_params, log_tags, log_artifact

    km = pipeline.named_steps.get("kmeans")
    km_params = km.get_params() if km is not None else {}

    with start_run("cluster_model"):
        log_params({
            "model_version": MODEL_VERSION,
            "k": k,
            "n_samples": n_samples,
            "n_features": len(FEATURE_COLS),
            "n_init": km_params.get("n_init"),
            "max_iter": km_params.get("max_iter"),
            "random_state": km_params.get("random_state"),
        })
        log_tags({
            "model_name": "cluster_model",
            "task": "unsupervised",
            "features": ",".join(FEATURE_COLS),
        })
        log_artifact(model_file)
        log_artifact(meta_path)


if __name__ == "__main__":
    print("Generating synthetic student data...")
    df = generate_synthetic_cluster_data(N_SAMPLES, RANDOM_SEED)
    print(f"  {len(df)} students | {len(FEATURE_COLS)} features\n")

    print("Running elbow method to confirm K=4...")
    elbow_method(df)

    print(f"\nTraining K-Means with K={K}...")
    pipeline = train(df, K)

    save_with_metadata(pipeline, k=K, n_samples=len(df))
