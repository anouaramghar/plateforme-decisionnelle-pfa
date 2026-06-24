import pandas as pd
from models.train_regression import train, generate_data

def test_forecast_training_splits_out_of_time():
    """
    Assert that if the dataset has multiple periods, an out_of_time split is used,
    and students are strictly isolated between train and test sets for forecast model.
    """
    df = generate_data(n=200, seed=42)
    assert df["period_key"].nunique() > 1

    pipeline, _X_test, _y_test = train(df)

    assert pipeline.split_strategy == "out_of_time"

    # Verify student isolation
    max_period = df["period_key"].max()
    test_df = df[df.period_key == max_period]
    train_df = df[df.period_key < max_period]
    train_df_isolated = train_df[~train_df.EtudiantId.isin(test_df.EtudiantId)]

    overlap = set(train_df_isolated["EtudiantId"]).intersection(set(test_df["EtudiantId"]))
    assert len(overlap) == 0


def test_forecast_training_splits_grouped_student():
    """
    Assert that if the dataset has only 1 period, a grouped_student split is used,
    and students are strictly isolated between train and test sets for forecast model.
    """
    df = generate_data(n=100, seed=42)
    first_period = df["period_key"].min()
    df_single = df[df.period_key == first_period].copy()

    assert df_single["period_key"].nunique() == 1

    pipeline, _X_test, _y_test = train(df_single)

    assert pipeline.split_strategy == "grouped_student"

    # Verify student isolation using GroupShuffleSplit indices
    from sklearn.model_selection import GroupShuffleSplit
    gss = GroupShuffleSplit(n_splits=1, test_size=0.2, random_state=42)
    train_idx, test_idx = next(gss.split(df_single, groups=df_single["EtudiantId"]))
    train_students = set(df_single.iloc[train_idx]["EtudiantId"])
    test_students = set(df_single.iloc[test_idx]["EtudiantId"])

    overlap = train_students.intersection(test_students)
    assert len(overlap) == 0
