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


# --- Production-scale integration tests (20 workers, 12 tasks) ---


def _get_assignments(result):
    """Extract assigned (worker_idx, task_idx, level) tuples from raw solution."""
    assigned = result.raw_solution_df[result.raw_solution_df["waarde"] == 1].copy()
    assigned["werknemer"] = assigned["werknemer"].astype(int)
    assigned["taak"] = assigned["taak"].astype(int)
    assigned["level"] = assigned["level"].astype(int)
    return assigned


ALL_OBJECTIVES = [OBJECTIVE_MAXIMIZE, OBJECTIVE_MINIMIZE, OBJECTIVE_HYBRID]


@pytest.mark.parametrize("objective", ALL_OBJECTIVES)
class TestProductionScaleSolver:
    """Integration tests using a realistic 20-worker / 12-task scenario."""

    def test_solves_optimal(self, production_present_workers, production_data_taken, objective):
        params = build_solver_parameters(production_present_workers, production_data_taken)
        result = build_and_solve(params, objective, production_data_taken, production_present_workers)
        assert result.status == "optimal", f"Expected optimal, got {result.status}"
        assert result.relaxation_attempt == 0
        assert result.warnings == []

    def test_each_worker_assigned_exactly_once(self, production_present_workers, production_data_taken, objective):
        params = build_solver_parameters(production_present_workers, production_data_taken)
        result = build_and_solve(params, objective, production_data_taken, production_present_workers)
        assigned = _get_assignments(result)

        worker_ids = assigned["werknemer"].tolist()
        assert len(worker_ids) == 20, f"Expected 20 assignments, got {len(worker_ids)}"
        assert len(worker_ids) == len(set(worker_ids)), "Duplicate worker assignments found"

    def test_task_headcount_satisfied(self, production_present_workers, production_data_taken, objective):
        params = build_solver_parameters(production_present_workers, production_data_taken)
        result = build_and_solve(params, objective, production_data_taken, production_present_workers)
        assigned = _get_assignments(result)

        for task_idx in production_data_taken.index:
            count = len(assigned[assigned["taak"] == task_idx])
            expected = production_data_taken.loc[task_idx, "Aantal"]
            task_name = production_data_taken.loc[task_idx, "Taken"]
            assert count == expected, f"{task_name}: expected {expected} workers, got {count}"

    def test_skill_eligibility_respected(self, production_present_workers, production_data_taken, objective):
        params = build_solver_parameters(production_present_workers, production_data_taken)
        result = build_and_solve(params, objective, production_data_taken, production_present_workers)
        assigned = _get_assignments(result)

        for _, row in assigned.iterrows():
            worker_idx = row["werknemer"]
            task_idx = row["taak"]
            level = row["level"]
            task_name = production_data_taken.loc[task_idx, "Taken"]
            actual_skill = production_present_workers.loc[worker_idx, task_name]
            assert actual_skill <= level, (
                f"Worker {worker_idx} has skill {actual_skill} on {task_name}, "
                f"but was assigned at level {level}"
            )

    def test_language_compatibility_on_warehousing(self, production_present_workers, production_data_taken, objective):
        params = build_solver_parameters(production_present_workers, production_data_taken)
        result = build_and_solve(params, objective, production_data_taken, production_present_workers)
        assigned = _get_assignments(result)

        # Find Warehousing task index (Samenwerken == 1)
        warehousing_idx = production_data_taken[
            production_data_taken["Taken"] == "Warehousing"
        ].index[0]
        warehousing_workers = assigned[assigned["taak"] == warehousing_idx]["werknemer"].tolist()

        # Every pair of workers on Warehousing must share a language
        for i, w1 in enumerate(warehousing_workers):
            for w2 in warehousing_workers[i + 1:]:
                nl_shared = (production_present_workers.loc[w1, "Nederlands"]
                             + production_present_workers.loc[w2, "Nederlands"]) == 2
                pl_shared = (production_present_workers.loc[w1, "Pools"]
                             + production_present_workers.loc[w2, "Pools"]) == 2
                assert nl_shared or pl_shared, (
                    f"Workers {w1} and {w2} on Warehousing share no language"
                )

    def test_minimum_skill_level_requirements(self, production_present_workers, production_data_taken, objective):
        params = build_solver_parameters(production_present_workers, production_data_taken)
        result = build_and_solve(params, objective, production_data_taken, production_present_workers)
        assigned = _get_assignments(result)

        for task_idx in production_data_taken.index:
            task_name = production_data_taken.loc[task_idx, "Taken"]
            min_l1 = production_data_taken.loc[task_idx, "Aantal_min_niveau_1"]
            if min_l1 > 0:
                l1_count = len(assigned[
                    (assigned["taak"] == task_idx) & (assigned["level"] == 1)
                ])
                assert l1_count >= min_l1, (
                    f"{task_name}: needs {min_l1} level-1 workers, got {l1_count}"
                )
