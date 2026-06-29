import numpy as np
import pandas as pd
import pytest

def test_forecast_no_leakage_of_target_into_features():
    """
    Assert that there is no leakage of the current/prior period's target (note_finale,
    which is next period's grade) into current features (moyenne_actuelle, taux_absence, nb_modules).
    """
    # Create raw records representing student performance across periods
    raw_df = pd.DataFrame([
        {"EtudiantId": 1, "period_key": 2025.1, "moyenne_generale": 14.0, "taux_absence": 0.02, "nb_modules": 6, "ecart_type_modules": 1.5, "nb_echecs_anterieurs": 0},
        {"EtudiantId": 1, "period_key": 2025.2, "moyenne_generale": 11.0, "taux_absence": 0.08, "nb_modules": 6, "ecart_type_modules": 2.5, "nb_echecs_anterieurs": 0},
    ])

    def process(df):
        df = df.sort_values(["EtudiantId", "period_key"])
        df["note_finale"] = df.groupby("EtudiantId")["moyenne_generale"].shift(-1)
        df = df.dropna(subset=["note_finale"]).copy()
        df = df.rename(columns={"moyenne_generale": "moyenne_actuelle"})
        return df

    original_processed = process(raw_df.copy())

    # In the original, current period features are: moyenne_actuelle = 14.0, target is 11.0
    assert original_processed.loc[0, "moyenne_actuelle"] == 14.0
    assert original_processed.loc[0, "note_finale"] == 11.0

    # Modify the future grade to 18.0
    modified_raw_df = raw_df.copy()
    modified_raw_df.loc[1, "moyenne_generale"] = 18.0

    modified_processed = process(modified_raw_df)

    # The current period features must remain completely unchanged
    assert modified_processed.loc[0, "moyenne_actuelle"] == 14.0
    assert modified_processed.loc[0, "taux_absence"] == original_processed.loc[0, "taux_absence"]
    assert modified_processed.loc[0, "nb_modules"] == original_processed.loc[0, "nb_modules"]

    # Only the target (note_finale) is updated
    assert modified_processed.loc[0, "note_finale"] == 18.0
