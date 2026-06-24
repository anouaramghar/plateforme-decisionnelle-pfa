"""
Training script for student segmentation (K-Means clustering).

Unlike the risk classifier, this is UNSUPERVISED — we don't have labels.
K-Means groups students by similarity and we interpret the groups afterward.

Usage:
    python models/train_clustering.py
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

RANDOM_SEED = 42
N_SAMPLES = 1000
K = 4  # number of clusters (chosen via elbow method below)
MODEL_VERSION = "1.8.0"
MODEL_PATH = SAVED_MODELS_DIR / "cluster_model.joblib"


# ─── 1. Generate synthetic student data ───────────────────────────────────────

def generate_data(n: int, seed: int) -> pd.DataFrame:
    """
    Same feature space as the risk model (no labels — unsupervised).

    Real-world profiles we expect K-Means to discover:
      - High performers: high moyenne, low absence
      - Struggling students: low moyenne, high absence
      - Average students: middle range
      - Engaged but underperforming: low grades but low absence
    """
    rng = np.random.default_rng(seed)

    moyenne = rng.uniform(0, 20, n)
    absence = rng.uniform(0, 0.6, n)
    nb_modules = rng.integers(3, 12, n)

    return pd.DataFrame({
        "moyenne_generale": moyenne,
        "taux_absence": absence,
        "nb_modules": nb_modules,
    })


# ─── 2. Choose K with the elbow method ────────────────────────────────────────

def elbow_method(df: pd.DataFrame) -> None:
    """
    Trains K-Means for K=2..10 and plots inertia (sum of squared distances
    to cluster centers). The "elbow" — where the curve bends — is the best K.

    We hardcode K=4 above, but this function lets you verify the choice.
    """
    import matplotlib
    matplotlib.use("Agg")  # headless — no display needed
    import matplotlib.pyplot as plt

    scaler = StandardScaler()
    X_scaled = scaler.fit_transform(df)

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


# ─── 3. Train the final K-Means pipeline ──────────────────────────────────────

def train(df: pd.DataFrame, k: int) -> Pipeline:
    """
    Wraps StandardScaler + KMeans in a Pipeline.

    Why scale before K-Means?
      K-Means uses Euclidean distance. Without scaling, a feature measured
      in large units (moyenne 0-20) would dominate features measured in
      small units (absence 0-1). Scaling puts all features on equal footing.

    Why n_init=10?
      K-Means starts with random centroids. Different starts can give
      different results. Running it 10 times and keeping the best avoids
      unlucky initializations.
    """
    X = df[["moyenne_generale", "taux_absence", "nb_modules"]]

    pipeline = Pipeline([
        ("scaler", StandardScaler()),
        ("kmeans", KMeans(
            n_clusters=k,
            random_state=RANDOM_SEED,
            n_init=10,          # run 10 times, keep the best
            max_iter=300,       # max iterations per run
        )),
    ])

    pipeline.fit(X)

    # ── Show cluster profiles ─────────────────────────────────────────────
    labels = pipeline.named_steps["kmeans"].labels_
    df_result = df.copy()
    df_result["cluster"] = labels

    print(f"\n=== Cluster Profiles (K={k}) ===")
    profile = df_result.groupby("cluster").agg(
        count=("cluster", "size"),
        avg_grade=("moyenne_generale", "mean"),
        avg_absence=("taux_absence", "mean"),
        avg_modules=("nb_modules", "mean"),
    ).round(2)
    print(profile.to_string())
    print("\nInterpret clusters by avg_grade and avg_absence:")
    print("  Low grade + high absence  -> at-risk students")
    print("  High grade + low absence  -> top performers")
    print("  Middle values             -> average / engaged groups")

    return pipeline


# ─── 4. Save the trained pipeline to disk ─────────────────────────────────────

def save(pipeline: Pipeline) -> None:
    save_with_metadata(pipeline, k=K, n_samples=0)


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
        "trained_at": datetime.now(timezone.utc).isoformat(),
    }
    meta_path = out_dir / "cluster_metadata.json"
    meta_path.write_text(json.dumps(metadata, indent=2), encoding="utf-8")
    print(f"Metadata saved -> {meta_path}")

    # ── MLflow tracking (best-effort, unsupervised — params + artifacts only) ─
    from mlflow_client import start_run, log_params, log_tags, log_artifact

    km = pipeline.named_steps.get("kmeans")
    km_params = km.get_params() if km is not None else {}

    with start_run("cluster_model"):
        log_params({
            "model_version": MODEL_VERSION,
            "k": k,
            "n_samples": n_samples,
            "n_init": km_params.get("n_init"),
            "max_iter": km_params.get("max_iter"),
            "random_state": km_params.get("random_state"),
        })
        log_tags({"model_name": "cluster_model", "task": "unsupervised"})
        log_artifact(model_file)
        log_artifact(meta_path)


# ─── Entry point ──────────────────────────────────────────────────────────────

if __name__ == "__main__":
    print("Generating synthetic student data...")
    df = generate_data(N_SAMPLES, RANDOM_SEED)
    print(f"  {len(df)} students\n")

    print("Running elbow method to confirm K=4...")
    elbow_method(df)

    print(f"\nTraining K-Means with K={K}...")
    pipeline = train(df, K)

    save(pipeline)
