import pandas as pd

from data.db_loader import _compute_nb_echecs_anterieurs, _compute_period_key


def test_compute_period_key_orders_s1_through_s9():
    keys = [_compute_period_key("2025/2026", f"S{i}") for i in range(1, 10)]

    assert keys == sorted(keys)
    assert len(set(keys)) == 9
    assert keys[0] == 2025.1
    assert keys[-1] == 2025.9


def test_compute_period_key_accepts_semestre_text():
    assert _compute_period_key("2025/2026", "Semestre 7") == 2025.7


def test_prior_failures_are_grouped_per_student():
    df = pd.DataFrame(
        [
            {"EtudiantId": 1, "period_key": 2025.1, "moyenne_generale": 9.0},
            {"EtudiantId": 1, "period_key": 2025.2, "moyenne_generale": 12.0},
            {"EtudiantId": 1, "period_key": 2025.3, "moyenne_generale": 8.0},
            {"EtudiantId": 2, "period_key": 2025.1, "moyenne_generale": 15.0},
            {"EtudiantId": 2, "period_key": 2025.2, "moyenne_generale": 7.0},
        ]
    )

    assert _compute_nb_echecs_anterieurs(df).tolist() == [0, 1, 1, 0, 0]
