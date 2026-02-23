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
