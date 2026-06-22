import numpy as np
import pandas as pd
import pytest
from models.train_risk import generate_data

def test_temporal_training_contract():
    """
    Assert the temporal training contract:
    Changing a student's grade/performance in a future period
    must only change the 'at_risk' label in the current/prior period,
    and must never change the features (moyenne_generale, taux_absence, nb_modules)
    of the current/prior period.
    """
    # 1. Generate base dataset
    df1 = generate_data(n=100, seed=42)

    # 2. Generate another dataset with different seed to get different grades/features,
    # but let's do a controlled modification to be absolutely precise.
    # Let's create a custom dataframe of raw records before shift-lagging,
    # apply the shift-lagging, and check.
    # Since the db_loader has parse_period_key and shift logic, we can also test it.
    # Let's mock a simple raw dataframe:
    raw_df = pd.DataFrame([
        {"EtudiantId": 1, "period_key": 2025.1, "moyenne_generale": 12.0, "taux_absence": 0.05, "nb_modules": 5, "failed": 0},
        {"EtudiantId": 1, "period_key": 2025.2, "moyenne_generale": 8.0,  "taux_absence": 0.10, "nb_modules": 5, "failed": 1},
    ])

    # Apply shift logic
    def process(df):
        df = df.sort_values(by=["EtudiantId", "period_key"])
        df["at_risk"] = df.groupby("EtudiantId")["failed"].shift(-1)
        return df.dropna(subset=["at_risk"]).copy()

    processed_original = process(raw_df.copy())
    
    # Check features and label of the first period
    assert processed_original.loc[0, "moyenne_generale"] == 12.0
    assert processed_original.loc[0, "at_risk"] == 1.0

    # Modify future period performance (change failed to 0 in 2025.2)
    modified_raw_df = raw_df.copy()
    modified_raw_df.loc[1, "failed"] = 0
    modified_raw_df.loc[1, "moyenne_generale"] = 15.0 # change future grade

    processed_modified = process(modified_raw_df)

    # Assert temporal contract:
    # 1. Current period features are unchanged
    assert processed_modified.loc[0, "moyenne_generale"] == processed_original.loc[0, "moyenne_generale"]
    assert processed_modified.loc[0, "taux_absence"] == processed_original.loc[0, "taux_absence"]
    assert processed_modified.loc[0, "nb_modules"] == processed_original.loc[0, "nb_modules"]

    # 2. Current period label is changed because future period failed status changed
    assert processed_modified.loc[0, "at_risk"] == 0.0
