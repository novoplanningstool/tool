"""Tests for postprocessing module."""

import pandas as pd

from postprocessing import (
    build_full_planning,
    compute_absent_workers,
    decode_solution,
    rename_board_columns,
    split_boards,
)


# --- decode_solution ---


class TestDecodeSolution:
    def test_replaces_indices_with_names(self, present_workers, data_taken):
        raw = pd.DataFrame({
            "x": ["x(1,0,0)", "x(1,1,1)", "x(2,2,1)"],
            "level": ["1", "1", "2"],
            "werknemer": ["0", "1", "2"],
            "taak": ["0", "1", "1"],
            "waarde": [1.0, 1.0, 1.0],
        })
        result = decode_solution(raw, present_workers, data_taken)
        assert "Alice" in result.values
        assert "Bob" in result.values
        assert "Charlie" in result.values
        assert set(result.index) == {"Ompakken", "Krimpen"}

    def test_filters_to_assigned_only(self, present_workers, data_taken):
        raw = pd.DataFrame({
            "x": ["x(1,0,0)", "x(1,1,0)"],
            "level": ["1", "1"],
            "werknemer": ["0", "1"],
            "taak": ["0", "0"],
            "waarde": [1.0, 0.0],
        })
        result = decode_solution(raw, present_workers, data_taken)
        # Only Alice (waarde=1) should appear for Ompakken
        row = result.loc["Ompakken"].dropna().tolist()
        assert "Alice" in row
        assert "Bob" not in row


# --- build_full_planning ---


class TestBuildFullPlanning:
    def test_concatenates_extra_and_solution(self):
        extra = pd.DataFrame({"w1": ["Alice"]}, index=["Zeelandia"])
        solution = pd.DataFrame({"w1": ["Bob"]}, index=["Ompakken"])
        result = build_full_planning([extra], solution)
        assert set(result.index) == {"Zeelandia", "Ompakken"}

    def test_empty_extras_returns_solution(self):
        solution = pd.DataFrame({"w1": ["Bob"]}, index=["Ompakken"])
        result = build_full_planning([], solution)
        pd.testing.assert_frame_equal(result, solution)


# --- split_boards ---


class TestSplitBoards:
    def _make_test_data(self):
        """Build test data for split_boards with Zeelandia included."""
        full = pd.DataFrame(
            {0: ["Alice", "Bob", "Charlie"]},
            index=["Laden/lossen Zeelandia", "Ompakken", "Krimpen"],
        )
        tasks = pd.DataFrame({
            "Taken": ["Ompakken", "Krimpen"],
            "Verdeling oud planbord": [1, 2],
        })
        onetime = pd.DataFrame()
        return full, tasks, onetime

    def test_left_right_split(self):
        full, tasks, onetime = self._make_test_data()
        left, right = split_boards(full, tasks, onetime)
        assert "Laden/lossen Zeelandia" in left.index
        assert "Ompakken" in left.index
        assert "Krimpen" in right.index

    def test_onetime_tasks_appended_to_left(self):
        full, tasks, _ = self._make_test_data()
        onetime = pd.DataFrame({0: ["Dave"]}, index=["SpecialTask"])
        left, right = split_boards(full, tasks, onetime)
        assert "SpecialTask" in left.index

    def test_nan_replaced_with_empty_string(self):
        full = pd.DataFrame(
            {0: ["Alice", "Bob"], 1: [None, "Charlie"]},
            index=["Laden/lossen Zeelandia", "Ompakken"],
        )
        tasks = pd.DataFrame({
            "Taken": ["Ompakken"],
            "Verdeling oud planbord": [1],
        })
        left, right = split_boards(full, tasks, pd.DataFrame())
        assert "" in left.values or left.isna().sum().sum() == 0


# --- compute_absent_workers ---


class TestComputeAbsentWorkers:
    def test_returns_absent_workers(self):
        data = pd.DataFrame({
            "Werknemers": ["Alice", "Bob", "Charlie"],
            "Aanwezig": [1, 0, 0],
        })
        result = compute_absent_workers(data, [])
        names = set(result[0])
        assert "Bob" in names
        assert "Charlie" in names
        assert "Alice" not in names

    def test_excludes_special_task_workers(self):
        data = pd.DataFrame({
            "Werknemers": ["Alice", "Bob"],
            "Aanwezig": [0, 0],
        })
        result = compute_absent_workers(data, ["Alice"])
        names = set(result[0])
        assert "Alice" not in names
        assert "Bob" in names

    def test_all_present_returns_empty(self):
        data = pd.DataFrame({
            "Werknemers": ["Alice", "Bob"],
            "Aanwezig": [1, 1],
        })
        result = compute_absent_workers(data, [])
        assert result.empty


# --- rename_board_columns ---


class TestRenameBoardColumns:
    def test_columns_renamed(self):
        df = pd.DataFrame({0: ["Alice"], 1: ["Bob"], 2: ["Charlie"]})
        result = rename_board_columns(df)
        assert list(result.columns) == [
            "Werknemer 1", "Werknemer 2", "Werknemer 3"]

    def test_single_column(self):
        df = pd.DataFrame({0: ["Alice"]})
        result = rename_board_columns(df)
        assert list(result.columns) == ["Werknemer 1"]
