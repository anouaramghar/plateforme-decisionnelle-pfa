"""
Training script for grade forecasting (XGBoost regression).

This predicts a student's FINAL average grade (note_finale) as a continuous
number given their mid-semester average, absence rate, and module count.

This is REGRESSION, not classification:
  - Classification: outputs a category (at-risk / not at-risk)
  - Regression: outputs a continuous number (predicted final grade 0-20)

Usage:
    python models/train_regression.py
"""

import numpy as np
import pandas as pd
from pathlib import Path
from sklearn.model_selection import train_test_split
from sklearn.metrics import mean_absolute_error, r2_score
from sklearn.preprocessing import StandardScaler
from sklearn.pipeline import Pipeline
import xgboost as xgb
import joblib

RANDOM_SEED = 42
N_SAMPLES = 1000
MODEL_PATH = Path(__file__).parent.parent / "saved_models" / "forecast_model.joblib"


# ─── 1. Generate synthetic student data ───────────────────────────────────────

def generate_data(n: int, seed: int) -> pd.DataFrame:
    """
    Creates realistic student records where the final grade is derived from
    the mid-semester average plus some noise and an absence penalty.

    The relationship we encode:
      note_finale ≈ moyenne_actuelle
                    - absence_penalty  (missing class hurts the final grade)
                    + small random noise

    This gives the model a realistic signal to learn from.
    """
    rng = np.random.default_rng(seed)

    moyenne_actuelle = rng.uniform(4, 18, n)   # current semester average
    taux_absence = rng.uniform(0, 0.5, n)      # absence rate 0-50%
    nb_modules = rng.integers(3, 12, n)        # 3 to 11 modules

    # Absence pulls the final grade down (up to -4 points at max absence)
    absence_penalty = taux_absence * 8.0

    # Add gaussian noise to make it a fuzzy prediction, not a perfect rule
    noise = rng.normal(0, 1.2, n)

    note_finale = moyenne_actuelle - absence_penalty + noise
    note_finale = np.clip(note_finale, 0, 20)  # keep within 0-20 scale

    return pd.DataFrame({
        "moyenne_actuelle": moyenne_actuelle,
        "taux_absence": taux_absence,
        "nb_modules": nb_modules,
        "note_finale": note_finale,
    })


# ─── 2. Build and train the regression model ──────────────────────────────────

def train(df: pd.DataFrame) -> Pipeline:
    """
    Same Pipeline pattern as the risk classifier, but using XGBRegressor.

    Key difference from the classifier:
      - XGBClassifier.predict_proba() → probability (0-1)
      - XGBRegressor.predict()        → continuous number (0-20)

    Evaluation metrics:
      - MAE (Mean Absolute Error): on average, how many grade points off?
        An MAE of 1.5 means we're ±1.5 points off on average. Good for a PFA.
      - R² (R-squared / coefficient of determination): how much of the variance
        in final grades does the model explain?
        R²=1.0 → perfect, R²=0.0 → same as just predicting the mean every time.
        Aim for R² > 0.80.
    """
    X = df[["moyenne_actuelle", "taux_absence", "nb_modules"]]
    y = df["note_finale"]

    X_train, X_test, y_train, y_test = train_test_split(
        X, y, test_size=0.2, random_state=RANDOM_SEED
    )

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
    # Clip predictions to valid grade range before scoring
    y_pred_clipped = np.clip(y_pred, 0, 20)

    mae = mean_absolute_error(y_test, y_pred_clipped)
    r2 = r2_score(y_test, y_pred_clipped)

    print("\n=== Forecast Model Evaluation ===")
    print(f"MAE  : {mae:.2f} grade points  (lower is better)")
    print(f"R²   : {r2:.3f}               (closer to 1.0 is better)")
    print("  MAE < 2.0 and R² > 0.80 are good targets for a PFA demo model")

    return pipeline


# ─── 3. Save the trained pipeline to disk ─────────────────────────────────────

def save(pipeline: Pipeline) -> None:
    MODEL_PATH.parent.mkdir(parents=True, exist_ok=True)
    joblib.dump(pipeline, MODEL_PATH)
    print(f"\nModel saved -> {MODEL_PATH}")


# ─── Entry point ──────────────────────────────────────────────────────────────

if __name__ == "__main__":
    print("Generating synthetic student data...")
    df = generate_data(N_SAMPLES, RANDOM_SEED)
    print(f"  {len(df)} students | avg final grade: {df['note_finale'].mean():.1f}/20\n")

    print("Training XGBoost regressor...")
    pipeline = train(df)

    save(pipeline)
