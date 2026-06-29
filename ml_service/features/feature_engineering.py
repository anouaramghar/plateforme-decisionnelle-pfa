"""
Feature engineering module — single source of truth for all ML features.

Centralises feature definitions, synthetic data generation, and shared
utilities used by the risk classifier, grade forecaster, and clustering
model. Eliminates the duplicate generate_data() functions that previously
lived in each train_*.py file.

Feature set (5 features):
  1. moyenne_generale       — average grade across modules (0–20)
  2. taux_absence           — absence rate as fraction of total hours (0–1)
  3. nb_modules             — number of modules enrolled
  4. ecart_type_modules     — std dev of grades across modules (instability)
  5. nb_echecs_anterieurs   — count of prior-period academic failures
"""

import numpy as np
import pandas as pd
from sklearn.metrics import fbeta_score, precision_recall_curve

SEED = 42
N_SAMPLES = 1000

FEATURE_COLS = [
    "moyenne_generale",
    "taux_absence",
    "nb_modules",
    "ecart_type_modules",
    "nb_echecs_anterieurs",
]

FEATURE_LABELS = {
    "moyenne_generale":    "Note moyenne",
    "taux_absence":        "Taux d'absence",
    "nb_modules":          "Nombre de modules",
    "ecart_type_modules":  "Variabilité des notes",
    "nb_echecs_anterieurs":"Échecs antérieurs",
}

FEATURE_RANGES = {
    "moyenne_generale":    (0.0, 20.0),
    "taux_absence":        (0.0, 1.0),
    "nb_modules":          (1, 12),
    "ecart_type_modules":  (0.0, 8.0),
    "nb_echecs_anterieurs":(0, 6),
}

CLUSTER_COLS = list(FEATURE_COLS)

TARGET_COL_RISK = "at_risk"
TARGET_COL_FORECAST = "note_finale"


# ─── Shared synthetic data generation ─────────────────────────────────────────

def _make_student_profiles(
    n_students: int,
    rng: np.random.Generator,
) -> tuple[np.ndarray, np.ndarray, np.ndarray, np.ndarray]:
    """
    Generates base student profiles with realistic inter-correlations.

    Returns (base_grades, base_absences, base_variability, base_failures).
    """
    base_grade = rng.normal(11.5, 3.0, n_students).clip(2, 18)
    base_absence = np.clip(
        0.02 + 0.35 * (1 - (base_grade - 2) / 16) + rng.normal(0, 0.06, n_students),
        0.0, 0.6,
    )
    base_variability = np.clip(
        1.0 + 3.0 * (base_absence / 0.6) + rng.normal(0, 0.5, n_students),
        0.3, 7.0,
    )
    base_failures = np.clip(
        np.round(2.5 * (1 - (base_grade - 2) / 16) + rng.normal(0, 0.5, n_students)),
        0, 5,
    ).astype(int)

    return base_grade, base_absence, base_variability, base_failures


def generate_synthetic_risk_data(
    n: int = N_SAMPLES,
    seed: int = SEED,
) -> pd.DataFrame:
    """
    Generates synthetic student-period records for the risk classifier.

    Each student has data over 2 periods. The target (at_risk) is 1 when the
    student's average drops below 10 in the *next* period.

    Correlations encoded:
      - Low grade + high absence + high variability → very high risk
      - Previous failures strongly amplify risk
      - Absence erodes grades over time
    """
    rng = np.random.default_rng(seed)
    n_students = max(n, 30)

    base_grade, base_absence, base_variability, base_failures = _make_student_profiles(n_students, rng)

    records = []
    for i in range(n_students):
        if i < n_students // 2:
            periods = [2025.1, 2025.2]
        else:
            periods = [2025.2, 2026.1]

        cumulative_failures = int(base_failures[i])
        for p_idx, p in enumerate(periods):
            grade = np.clip(
                base_grade[i] - base_absence[i] * 3.0 * (p_idx + 1) + rng.normal(0, 1.2),
                0, 20,
            )
            absence = np.clip(base_absence[i] + rng.normal(0, 0.04), 0, 0.7)
            nb_mods = int(rng.integers(6, 9))  # real ENIAD curricula: 7-8 per semester

            ecart_type = np.clip(base_variability[i] + rng.normal(0, 0.3), 0.3, 7.0)

            records.append({
                "EtudiantId": i + 1,
                "period_key": p,
                "moyenne_generale": round(grade, 2),
                "taux_absence": round(absence, 4),
                "nb_modules": nb_mods,
                "ecart_type_modules": round(ecart_type, 2),
                "nb_echecs_anterieurs": cumulative_failures,
            })

            if grade < 10.0:
                cumulative_failures += 1

    df = pd.DataFrame(records)
    df = df.sort_values(["EtudiantId", "period_key"])

    df["failed"] = (df["moyenne_generale"] < 10.0).astype(int)
    df["future_failed"] = df.groupby("EtudiantId")["failed"].shift(-1)
    df = df.dropna(subset=["future_failed"]).copy()
    df[TARGET_COL_RISK] = df["future_failed"].astype(int)
    df = df.drop(columns=["failed", "future_failed"])

    feature_cols = FEATURE_COLS + [TARGET_COL_RISK, "EtudiantId", "period_key"]
    return df[feature_cols]


def generate_synthetic_forecast_data(
    n: int = N_SAMPLES,
    seed: int = SEED,
) -> pd.DataFrame:
    """
    Generates synthetic data for the grade regression model.

    Predicts a student's final grade (note_finale) at the *next* period
    given current-period features.

    Current features are labelled moyenne_actuelle / taux_absence / etc.
    The target is note_finale (0–20).
    """
    rng = np.random.default_rng(seed)
    n_students = max(n, 30)

    base_grade, base_absence, base_variability, base_failures = _make_student_profiles(n_students, rng)

    records = []
    for i in range(n_students):
        if i < n_students // 2:
            periods = [2025.1, 2025.2]
        else:
            periods = [2025.2, 2026.1]

        cumulative_failures = int(base_failures[i])
        for p_idx, p in enumerate(periods):
            current_grade = np.clip(
                base_grade[i] - base_absence[i] * 2.5 * p_idx + rng.normal(0, 1.2),
                0, 20,
            )

            future_grade = np.clip(
                current_grade
                - base_absence[i] * 4.0
                - base_variability[i] * 0.5
                - cumulative_failures * 1.2
                + rng.normal(0, 1.5),
                0, 20,
            )

            absence = np.clip(base_absence[i] + rng.normal(0, 0.04), 0, 0.7)
            nb_mods = int(rng.integers(6, 9))
            ecart_type = np.clip(base_variability[i] + rng.normal(0, 0.3), 0.3, 7.0)

            records.append({
                "EtudiantId": i + 1,
                "period_key": p,
                "moyenne_actuelle": round(current_grade, 2),
                "taux_absence": round(absence, 4),
                "nb_modules": nb_mods,
                "ecart_type_modules": round(ecart_type, 2),
                "nb_echecs_anterieurs": cumulative_failures,
                TARGET_COL_FORECAST: round(future_grade, 2),
            })

            if current_grade < 10.0:
                cumulative_failures += 1

    df = pd.DataFrame(records)
    df = df.sort_values(["EtudiantId", "period_key"])
    return df


def generate_synthetic_cluster_data(
    n: int = N_SAMPLES,
    seed: int = SEED,
) -> pd.DataFrame:
    """
    Generates synthetic unlabeled data for K-Means clustering.

    Produces 4 natural clusters:
      - High performers:     high grade, low absence, low variability
      - Struggling:          low grade, high absence, high variability
      - Average:             mid-range on all features
      - Engaged-but-stuck:   low grade despite low absence, low variability
    """
    rng = np.random.default_rng(seed)
    n_per_cluster = max(n // 4, 10)

    clusters = []

    high_perf = pd.DataFrame({
        "moyenne_generale": rng.normal(15, 1.5, n_per_cluster).clip(12, 20),
        "taux_absence": rng.normal(0.03, 0.02, n_per_cluster).clip(0, 0.1),
        "nb_modules": rng.integers(6, 11, n_per_cluster),
        "ecart_type_modules": rng.normal(1.0, 0.5, n_per_cluster).clip(0.2, 3.0),
        "nb_echecs_anterieurs": np.zeros(n_per_cluster, dtype=int),
    })
    struggling = pd.DataFrame({
        "moyenne_generale": rng.normal(7, 2.0, n_per_cluster).clip(0, 10),
        "taux_absence": rng.normal(0.35, 0.1, n_per_cluster).clip(0.15, 0.6),
        "nb_modules": rng.integers(3, 7, n_per_cluster),
        "ecart_type_modules": rng.normal(4.5, 1.0, n_per_cluster).clip(2.0, 7.0),
        "nb_echecs_anterieurs": rng.integers(2, 5, n_per_cluster),
    })
    average = pd.DataFrame({
        "moyenne_generale": rng.normal(11, 1.5, n_per_cluster).clip(8, 14),
        "taux_absence": rng.normal(0.12, 0.06, n_per_cluster).clip(0.02, 0.3),
        "nb_modules": rng.integers(4, 9, n_per_cluster),
        "ecart_type_modules": rng.normal(2.5, 0.8, n_per_cluster).clip(0.5, 5.0),
        "nb_echecs_anterieurs": rng.integers(0, 2, n_per_cluster),
    })
    engaged_stuck = pd.DataFrame({
        "moyenne_generale": rng.normal(8, 1.0, n_per_cluster).clip(5, 10),
        "taux_absence": rng.normal(0.04, 0.03, n_per_cluster).clip(0, 0.12),
        "nb_modules": rng.integers(6, 11, n_per_cluster),
        "ecart_type_modules": rng.normal(1.5, 0.6, n_per_cluster).clip(0.3, 3.5),
        "nb_echecs_anterieurs": rng.integers(1, 3, n_per_cluster),
    })

    df = pd.concat([high_perf, struggling, average, engaged_stuck], ignore_index=True)
    df["moyenne_generale"] = df["moyenne_generale"].round(2)
    df["taux_absence"] = df["taux_absence"].round(4)
    df["ecart_type_modules"] = df["ecart_type_modules"].round(2)
    return df[FEATURE_COLS]


# ─── Threshold optimisation for Recall ────────────────────────────────────────

def find_optimal_threshold(
    y_true: np.ndarray,
    y_proba: np.ndarray,
    beta: float = 2.0,
) -> dict:
    """
    Finds the optimal decision threshold for the risk classifier.

    Returns the threshold that maximises F-beta score (default beta=2,
    which weights recall 2× higher than precision), plus Youden's J and the
    default 0.5 threshold for comparison.

    Returns a dict with keys:
      - optimal_threshold     : threshold that maximises F-beta
      - optimal_fbeta         : F-beta at that threshold
      - optimal_recall        : recall at that threshold
      - optimal_precision     : precision at that threshold
      - youden_threshold      : threshold that maximises Youden's J (TPR - FPR)
      - youden_j              : Youden's J at that threshold
      - baseline_threshold    : 0.5 (default)
      - baseline_recall       : recall at 0.5
      - baseline_precision    : precision at 0.5
      - chosen_threshold      : the recommended threshold
    """
    from sklearn.metrics import recall_score, precision_score

    precisions, recalls, thresholds = precision_recall_curve(y_true, y_proba)

    best_fbeta = 0
    best_threshold = 0.5
    best_recall = 0
    best_precision = 0

    for i in range(len(thresholds)):
        if precisions[i] == 0 and recalls[i] == 0:
            continue
        fbeta = fbeta_score(y_true, (y_proba >= thresholds[i]).astype(int), beta=beta, zero_division=0)
        if fbeta > best_fbeta:
            best_fbeta = fbeta
            best_threshold = thresholds[i]
            best_recall = recalls[i]
            best_precision = precisions[i]

    y_pred_default = (y_proba >= 0.5).astype(int)
    baseline_recall = recall_score(y_true, y_pred_default, zero_division=0)
    baseline_precision = precision_score(y_true, y_pred_default, zero_division=0)

    youden_j = 0
    youden_threshold = 0.5
    for i in range(len(thresholds)):
        tpr = recalls[i]
        fpr = 1 - precisions[i] if precisions[i] > 0 else 0
        j = tpr - fpr
        if j > youden_j:
            youden_j = j
            youden_threshold = thresholds[i]

    chosen = best_threshold

    return {
        "optimal_threshold": round(float(best_threshold), 4),
        "optimal_fbeta": round(float(best_fbeta), 4),
        "optimal_recall": round(float(best_recall), 4),
        "optimal_precision": round(float(best_precision), 4),
        "youden_threshold": round(float(youden_threshold), 4),
        "youden_j": round(float(youden_j), 4),
        "baseline_threshold": 0.5,
        "baseline_recall": round(float(baseline_recall), 4),
        "baseline_precision": round(float(baseline_precision), 4),
        "chosen_threshold": round(float(chosen), 4),
    }
