"""Tests for solver module."""

import numpy as np
import pandas as pd
import pytest

from data_loading import process_remaining_skill_levels
from solver import SolverParams, SolveResult, build_and_solve, build_solver_parameters

OBJECTIVE_MAXIMIZE = "Iedereen doet waar hij het beste in is"
OBJECTIVE_MINIMIZE = "Iedereen staat zo veel mogelijk op een machine waar hij nog over moet leren"
OBJECTIVE_HYBRID = "Op de belangrijke taken staan goede mensen, op de rest staan beginners"


# --- build_solver_parameters ---


class TestBuildSolverParameters:
    def test_returns_solver_params(self, present_workers, data_taken):
        result = build_solver_parameters(present_workers, data_taken)
        assert isinstance(result, SolverParams)

    def test_skill_index_tuples_length(self, present_workers, data_taken):
        params = build_solver_parameters(present_workers, data_taken)
        # 3 levels * 3 workers * 2 tasks = 18
        assert len(params.skill_index_tuples) == 3 * 3 * 2

    def test_skill_eligible_for_expert(self, present_workers, data_taken):
        """Alice has level 1 on Ompakken -> eligible at levels 1, 2, 3."""
        params = build_solver_parameters(present_workers, data_taken)
        alice_idx = 0
        ompakken_idx = 0
        for level in [1, 2, 3]:
            idx = params.skill_index_tuples.index((level, alice_idx, ompakken_idx))
            assert params.skill_eligible[idx] == 1

    def test_skill_eligible_for_beginner(self, present_workers, data_taken):
        """Charlie has level 3 on Ompakken -> eligible only at level 3."""
        params = build_solver_parameters(present_workers, data_taken)
        charlie_idx = 2
        ompakken_idx = 0
        for level in [1, 2]:
            idx = params.skill_index_tuples.index((level, charlie_idx, ompakken_idx))
            assert params.skill_eligible[idx] == 0
        idx = params.skill_index_tuples.index((3, charlie_idx, ompakken_idx))
        assert params.skill_eligible[idx] == 1

    def test_language_compatible_same_language(self, present_workers, data_taken):
        """Alice(NL) + Bob(NL) share Dutch -> compatible (0)."""
        params = build_solver_parameters(present_workers, data_taken)
        alice_idx, bob_idx = 0, 1
        idx = params.language_index_tuples.index((alice_idx, bob_idx))
        assert params.language_incompatible[idx] == 0

    def test_language_incompatible_mixed(self, present_workers, data_taken):
        """Alice(NL) + Charlie(PL) share no language -> incompatible (1)."""
        params = build_solver_parameters(present_workers, data_taken)
        alice_idx, charlie_idx = 0, 2
        idx = params.language_index_tuples.index((alice_idx, charlie_idx))
        assert params.language_incompatible[idx] == 1


# --- build_and_solve ---


class TestBuildAndSolve:
    def test_optimal_solution_found(self, present_workers, data_taken):
        params = build_solver_parameters(present_workers, data_taken)
        result = build_and_solve(params, OBJECTIVE_MAXIMIZE, data_taken, present_workers)
        assert result.status == "optimal"

    def test_each_worker_assigned_once(self, present_workers, data_taken):
        params = build_solver_parameters(present_workers, data_taken)
        result = build_and_solve(params, OBJECTIVE_MAXIMIZE, data_taken, present_workers)

        assigned = result.raw_solution_df[result.raw_solution_df["waarde"] == 1]
        worker_ids = assigned["werknemer"].tolist()
        assert len(worker_ids) == len(set(worker_ids))

    def test_task_headcount_satisfied(self, present_workers, data_taken):
        params = build_solver_parameters(present_workers, data_taken)
        result = build_and_solve(params, OBJECTIVE_MAXIMIZE, data_taken, present_workers)

        assigned = result.raw_solution_df[result.raw_solution_df["waarde"] == 1]
        for task_idx in data_taken.index:
            count = len(assigned[assigned["taak"] == str(task_idx)])
            expected = data_taken.loc[task_idx, "Aantal"]
            assert count == expected

    def test_all_three_objectives_solve(self, present_workers, data_taken):
        """All three objective functions should produce an optimal result."""
        params = build_solver_parameters(present_workers, data_taken)
        for obj in [OBJECTIVE_MAXIMIZE, OBJECTIVE_MINIMIZE, OBJECTIVE_HYBRID]:
            result = build_and_solve(params, obj, data_taken, present_workers)
            assert result.status == "optimal", f"Failed for objective: {obj}"

    def test_infeasible_with_level4_only_worker(self):
        """A worker with level 4 on all tasks makes the problem infeasible."""
        workers = pd.DataFrame({
            "Werknemers": ["Alice", "OnlyFour"],
            "Aanwezig": [1, 1],
            "Nederlands": [1, 1],
            "Pools": [0, 0],
            "TaskA": [1, 4],
            "Vrije dagen": [None, None],
        })
        tasks = pd.DataFrame({
            "Taken": ["TaskA"],
            "Aan": [1],
            "Aantal": [2],
            "Verdeling oud planbord": [1],
            "Aantal_min_niveau_1": [0],
            "Aantal_min_niveau_2": [0],
            "Aantal_min_niveau_3": [0],
            "Rest_min_niveau": [3],
            "Samenwerken": [0],
        })
        pw = workers.reset_index(drop=True)
        dt = tasks[tasks["Aan"] == 1].copy().reset_index(drop=True)
        process_remaining_skill_levels(dt)

        params = build_solver_parameters(pw, dt)
        result = build_and_solve(params, OBJECTIVE_MAXIMIZE, dt, pw)
        assert result.status == "infeasible"
        assert "OnlyFour" in result.level4_only_workers

    def test_relaxation_removes_language_constraint(self):
        """When language constraint alone causes infeasibility, solver relaxes it."""
        workers = pd.DataFrame({
            "Werknemers": ["Dutch", "Polish"],
            "Aanwezig": [1, 1],
            "Nederlands": [1, 0],
            "Pools": [0, 1],
            "CollabTask": [1, 1],
            "Vrije dagen": [None, None],
        })
        tasks = pd.DataFrame({
            "Taken": ["CollabTask"],
            "Aan": [1],
            "Aantal": [2],
            "Verdeling oud planbord": [1],
            "Aantal_min_niveau_1": [0],
            "Aantal_min_niveau_2": [0],
            "Aantal_min_niveau_3": [0],
            "Rest_min_niveau": [3],
            "Samenwerken": [1],  # requires language compatibility
        })
        pw = workers.reset_index(drop=True)
        dt = tasks[tasks["Aan"] == 1].copy().reset_index(drop=True)
        process_remaining_skill_levels(dt)

        params = build_solver_parameters(pw, dt)
        result = build_and_solve(params, OBJECTIVE_MAXIMIZE, dt, pw)
        assert result.status == "relaxed"
        assert result.relaxation_attempt >= 1
        assert len(result.warnings) > 0


# --- Dataclass defaults ---


class TestSolveResultDefaults:
    def test_default_fields(self):
        result = SolveResult(status="optimal", raw_solution_df=pd.DataFrame())
        assert result.warnings == []
        assert result.level4_only_workers == []
        assert result.level4_only_tasks == []
        assert result.relaxation_attempt == 0
