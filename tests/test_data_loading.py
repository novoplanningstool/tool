"""Tests for data_loading module."""

from datetime import datetime
from unittest.mock import patch

import pandas as pd
import pytest

from data_loading import (
    add_temp_workers,
    build_task_worker_map,
    compute_default_day,
    process_remaining_skill_levels,
    validate_task_columns,
)


# --- validate_task_columns ---


class TestValidateTaskColumns:
    def test_consistent_columns_returns_empty_set(self, werknemers_df, taken_df, uitzendkracht_df):
        result = validate_task_columns(werknemers_df, taken_df, uitzendkracht_df)
        assert result == set()

    def test_extra_column_in_werknemers_detected(self, werknemers_df, taken_df, uitzendkracht_df):
        werknemers_df["ExtraTask"] = [1, 2, 3]
        result = validate_task_columns(werknemers_df, taken_df, uitzendkracht_df)
        assert "ExtraTask" in result

    def test_missing_column_in_uitzendkracht_detected(self, werknemers_df, taken_df, uitzendkracht_df):
        uitzendkracht_df = uitzendkracht_df.drop(columns=["Ompakken"])
        result = validate_task_columns(werknemers_df, taken_df, uitzendkracht_df)
        assert "Ompakken" in result


# --- compute_default_day ---


class TestComputeDefaultDay:
    def test_monday_returns_1(self):
        with patch("data_loading.datetime") as mock_dt:
            mock_dt.datetime.today.return_value = datetime(2026, 2, 16)  # Monday
            assert compute_default_day() == 1

    def test_wednesday_returns_3(self):
        with patch("data_loading.datetime") as mock_dt:
            mock_dt.datetime.today.return_value = datetime(2026, 2, 18)  # Wednesday
            assert compute_default_day() == 3

    def test_thursday_returns_4(self):
        with patch("data_loading.datetime") as mock_dt:
            mock_dt.datetime.today.return_value = datetime(2026, 2, 19)  # Thursday
            assert compute_default_day() == 4

    def test_friday_returns_0(self):
        with patch("data_loading.datetime") as mock_dt:
            mock_dt.datetime.today.return_value = datetime(2026, 2, 20)  # Friday
            assert compute_default_day() == 0

    def test_saturday_returns_0(self):
        with patch("data_loading.datetime") as mock_dt:
            mock_dt.datetime.today.return_value = datetime(2026, 2, 21)  # Saturday
            assert compute_default_day() == 0

    def test_sunday_returns_0(self):
        with patch("data_loading.datetime") as mock_dt:
            mock_dt.datetime.today.return_value = datetime(2026, 2, 22)  # Sunday
            assert compute_default_day() == 0


# --- add_temp_workers ---


class TestAddTempWorkers:
    def test_adds_correct_number_of_rows(self, werknemers_df, uitzendkracht_df):
        result = add_temp_workers(werknemers_df, uitzendkracht_df, count=2)
        assert len(result) == len(werknemers_df) + 2

    def test_names_follow_pattern(self, werknemers_df, uitzendkracht_df):
        result = add_temp_workers(werknemers_df, uitzendkracht_df, count=2)
        names = result["Werknemers"].tolist()
        assert names[-2] == "Uitzendkracht 1"
        assert names[-1] == "Uitzendkracht 2"

    def test_skill_levels_match_template(self, werknemers_df, uitzendkracht_df):
        result = add_temp_workers(werknemers_df, uitzendkracht_df, count=1)
        temp_row = result.iloc[-1]
        assert temp_row["Ompakken"] == uitzendkracht_df.iloc[0]["Ompakken"]
        assert temp_row["Krimpen"] == uitzendkracht_df.iloc[0]["Krimpen"]

    def test_start_index_parameter(self, werknemers_df, uitzendkracht_df):
        result = add_temp_workers(werknemers_df, uitzendkracht_df, count=2, start_index=5)
        names = result["Werknemers"].tolist()
        assert names[-2] == "Uitzendkracht 5"
        assert names[-1] == "Uitzendkracht 6"

    def test_zero_count_returns_unchanged(self, werknemers_df, uitzendkracht_df):
        result = add_temp_workers(werknemers_df, uitzendkracht_df, count=0)
        pd.testing.assert_frame_equal(result, werknemers_df)


# --- build_task_worker_map ---


class TestBuildTaskWorkerMap:
    def test_basic_mapping(self):
        input_df = pd.DataFrame({0: ["TaskA", "TaskA", "TaskB"], 1: ["Alice", "Bob", "Charlie"]})
        result = build_task_worker_map(input_df)

        assert set(result.index) == {"TaskA", "TaskB"}
        assert list(result.loc["TaskA"].dropna()) == ["Alice", "Bob"]
        assert list(result.loc["TaskB"].dropna()) == ["Charlie"]

    def test_single_task_single_worker(self):
        input_df = pd.DataFrame({0: ["TaskA"], 1: ["Alice"]})
        result = build_task_worker_map(input_df)
        assert result.shape == (1, 1)
        assert result.loc["TaskA", 0] == "Alice"


# --- process_remaining_skill_levels ---


class TestProcessRemainingSkillLevels:
    def test_distributes_rest_to_correct_column(self):
        df = pd.DataFrame({
            "Aantal": [3],
            "Aantal_min_niveau_1": [1],
            "Aantal_min_niveau_2": [0],
            "Aantal_min_niveau_3": [0],
            "Rest_min_niveau": [3],
        })
        process_remaining_skill_levels(df)
        assert df.loc[0, "Aantal_min_niveau_3"] == 2

    def test_no_rest_needed(self):
        df = pd.DataFrame({
            "Aantal": [3],
            "Aantal_min_niveau_1": [1],
            "Aantal_min_niveau_2": [1],
            "Aantal_min_niveau_3": [1],
            "Rest_min_niveau": [3],
        })
        process_remaining_skill_levels(df)
        assert df.loc[0, "Aantal_min_niveau_1"] == 1
        assert df.loc[0, "Aantal_min_niveau_2"] == 1
        assert df.loc[0, "Aantal_min_niveau_3"] == 1

    def test_modifies_in_place(self):
        df = pd.DataFrame({
            "Aantal": [2],
            "Aantal_min_niveau_1": [1],
            "Aantal_min_niveau_2": [0],
            "Aantal_min_niveau_3": [0],
            "Rest_min_niveau": [2],
        })
        result = process_remaining_skill_levels(df)
        assert result is None
        assert df.loc[0, "Aantal_min_niveau_2"] == 1
