"""Shared test fixtures providing realistic sample DataFrames."""

import pandas as pd
import pytest

from data_loading import process_remaining_skill_levels

TASK_NAMES = ["Ompakken", "Krimpen"]


@pytest.fixture
def werknemers_df():
    """3 employees with skill levels for 2 tasks.

    Alice & Bob speak Dutch, Charlie speaks Polish.
    Skill levels: 1=expert, 2=experienced, 3=beginner, 4=cannot do.
    """
    return pd.DataFrame({
        "Werknemers": ["Alice", "Bob", "Charlie"],
        "Aanwezig": [1, 1, 1],
        "Nederlands": [1, 1, 0],
        "Pools": [0, 0, 1],
        "Ompakken": [1, 2, 3],
        "Krimpen": [2, 1, 2],
        "Vrije dagen": [None, None, None],
    })


@pytest.fixture
def taken_df():
    """2 tasks: Ompakken (1 worker, left board) and Krimpen (2 workers, right board)."""
    return pd.DataFrame({
        "Taken": ["Ompakken", "Krimpen"],
        "Aan": [1, 1],
        "Aantal": [1, 2],
        "Verdeling oud planbord": [1, 2],
        "Aantal_min_niveau_1": [1, 0],
        "Aantal_min_niveau_2": [0, 1],
        "Aantal_min_niveau_3": [0, 0],
        "Rest_min_niveau": [3, 3],
        "Samenwerken": [0, 0],
    })


@pytest.fixture
def uitzendkracht_df():
    """Temp worker template — skill level 3 on both tasks."""
    return pd.DataFrame({
        "Werknemers": ["Uitzendkracht"],
        "Aanwezig": [1],
        "Nederlands": [1],
        "Pools": [1],
        "Ompakken": [3],
        "Krimpen": [3],
        "Vrije dagen": [None],
    })


@pytest.fixture
def present_workers(werknemers_df):
    """Employees filtered to Aanwezig==1, with reset index."""
    pw = werknemers_df[werknemers_df["Aanwezig"] == 1].copy()
    return pw.reset_index(drop=True)


@pytest.fixture
def data_taken(taken_df):
    """Tasks filtered to Aan==1, with remaining skill levels distributed."""
    dt = taken_df[taken_df["Aan"] == 1].copy()
    dt = dt.reset_index(drop=True)
    process_remaining_skill_levels(dt)
    return dt


# ---------------------------------------------------------------------------
# Production-scale fixtures (20 workers, 12 tasks)
# Based on real sample data with anonymized employee names.
# ---------------------------------------------------------------------------

PRODUCTION_TASK_NAMES = [
    "Pallets wassen", "Ompakken", "Lijmen deur 11", "Warehousing",
    "Vrachtwagen 79-BKX-1", "Vrachtwagen BR-NR-81", "Locatie Extern",
    "Voorman golfkarton", "Kratjes wikkelen", "Stansmachine 1",
    "Schneider hoekstukken", "Plotter",
]


@pytest.fixture
def production_werknemers_df():
    """20 workers with skill levels for 12 tasks.

    Language mix: 3 Polish-only, 2 bilingual (NL+PL), 15 Dutch-only.
    Skill levels: 1=expert, 2=experienced, 3=beginner, 4=cannot do.
    """
    #                                                PW  OM  L11 WAR V79 VBR LOC VGK KW  ST1 SCH PLO
    skills = [
        ("Worker_01", 1, 0, [4,  4,  4,  1,  4,  4,  4,  4,  3,  4,  4,  4]),
        ("Worker_02", 1, 0, [4,  4,  4,  4,  2,  1,  4,  4,  3,  4,  4,  4]),
        ("Worker_03", 1, 0, [3,  4,  4,  2,  4,  4,  4,  1,  3,  4,  4,  4]),
        ("Worker_04", 1, 0, [4,  4,  4,  1,  4,  4,  2,  2,  4,  4,  4,  4]),
        ("Worker_05", 1, 0, [4,  4,  4,  1,  4,  4,  1,  4,  2,  4,  4,  4]),
        ("Worker_06", 1, 0, [4,  3,  1,  4,  4,  4,  4,  4,  4,  4,  4,  4]),
        ("Worker_07", 1, 0, [4,  2,  1,  4,  4,  4,  4,  4,  3,  3,  3,  4]),
        ("Worker_08", 0, 1, [3,  1,  2,  3,  4,  4,  4,  4,  2,  4,  4,  4]),
        ("Worker_09", 1, 0, [1,  4,  4,  3,  4,  4,  4,  4,  1,  4,  4,  4]),
        ("Worker_10", 1, 0, [4,  3,  3,  4,  4,  4,  4,  4,  4,  1,  2,  1]),
        ("Worker_11", 1, 0, [4,  3,  3,  4,  4,  4,  4,  4,  4,  1,  2,  2]),
        ("Worker_12", 1, 1, [4,  1,  2,  4,  4,  4,  4,  4,  4,  2,  2,  4]),
        ("Worker_13", 1, 1, [4,  1,  2,  4,  4,  4,  4,  4,  4,  2,  2,  4]),
        ("Worker_14", 1, 0, [4,  2,  1,  4,  4,  4,  4,  4,  4,  3,  3,  4]),
        ("Worker_15", 1, 0, [4,  2,  2,  4,  4,  4,  4,  4,  4,  2,  2,  2]),
        ("Worker_16", 1, 0, [4,  2,  3,  4,  4,  4,  4,  4,  4,  1,  1,  4]),
        ("Worker_17", 0, 1, [4,  1,  3,  4,  4,  4,  4,  4,  4,  4,  1,  4]),
        ("Worker_18", 1, 0, [4,  4,  4,  4,  1,  2,  4,  4,  3,  4,  4,  4]),
        ("Worker_19", 0, 1, [4,  3,  3,  4,  4,  4,  4,  4,  4,  2,  1,  4]),
        ("Worker_20", 1, 0, [4,  3,  2,  4,  4,  4,  4,  4,  4,  2,  2,  4]),
    ]
    data = {
        "Werknemers": [s[0] for s in skills],
        "Aanwezig": [1] * 20,
        "Nederlands": [s[1] for s in skills],
        "Pools": [s[2] for s in skills],
    }
    for i, task in enumerate(PRODUCTION_TASK_NAMES):
        data[task] = [s[3][i] for s in skills]
    data["Vrije dagen"] = [None] * 20
    return pd.DataFrame(data)


@pytest.fixture
def production_taken_df():
    """12 tasks with realistic headcounts and constraints.

    Total headcount = 20 (matches worker count).
    Warehousing has Samenwerken=1 (language collaboration required).
    """
    return pd.DataFrame({
        "Taken": PRODUCTION_TASK_NAMES,
        "Aan": [1] * 12,
        "Aantal":                [1, 5, 3, 2, 1, 1, 1, 1, 1, 1, 2, 1],
        "Verdeling oud planbord": [1, 1, 1, 1, 1, 1, 1, 2, 1, 2, 2, 2],
        "Aantal_min_niveau_1":   [1, 1, 2, 1, 1, 1, 0, 0, 0, 1, 0, 0],
        "Aantal_min_niveau_2":   [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        "Aantal_min_niveau_3":   [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        "Rest_min_niveau":       [3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3],
        "Samenwerken":           [0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0],
    })


@pytest.fixture
def production_present_workers(production_werknemers_df):
    """Production workers filtered to Aanwezig==1, with reset index."""
    pw = production_werknemers_df[production_werknemers_df["Aanwezig"] == 1].copy()
    return pw.reset_index(drop=True)


@pytest.fixture
def production_data_taken(production_taken_df):
    """Production tasks filtered to Aan==1, with remaining skill levels distributed."""
    dt = production_taken_df[production_taken_df["Aan"] == 1].copy()
    dt = dt.reset_index(drop=True)
    process_remaining_skill_levels(dt)
    return dt
