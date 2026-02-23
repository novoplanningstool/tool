"""LP model building, solving, and fallback constraint relaxation."""

from dataclasses import dataclass, field
from itertools import product

import numpy as np
import pandas as pd
from pulp import LpProblem, LpVariable, lpSum, LpMinimize, LpMaximize, LpBinary, LpInteger, PULP_CBC_CMD


@dataclass
class SolverParams:
    """Bundles all pre-computed arrays and indices needed by the MIP model."""
    levels: range
    taken: pd.Index
    werknemers: pd.Index
    skill_matrix_df: pd.DataFrame
    skill_index_tuples: list
    skill_eligible: np.ndarray
    teamsize_index_tuples: list
    min_workers_per_level: np.ndarray
    language_index_tuples: list
    language_incompatible: np.ndarray


@dataclass
class SolveResult:
    """Bundles solver output."""
    status: str  # 'optimal', 'relaxed', 'infeasible', 'other'
    raw_solution_df: pd.DataFrame
    relaxation_attempt: int = 0
    warnings: list = field(default_factory=list)
    level4_only_workers: list = field(default_factory=list)
    level4_only_tasks: list = field(default_factory=list)


def build_solver_parameters(present_workers, data_taken):
    """Build all pre-computed arrays and indices needed by the MIP model.

    Returns a SolverParams dataclass.
    """
    skill_matrix_df = present_workers.loc[:, data_taken.Taken]

    levels = range(1, 4)
    taken = data_taken.index
    werknemers = present_workers.index

    # parameter: does a worker have a certain level on a task?
    skill_index_tuples = list(product(levels, werknemers, taken))
    skill_eligible = np.zeros(len(skill_index_tuples))

    for taak in taken:
        naam_taak = data_taken.loc[taak, 'Taken']
        for werknemer in werknemers:
            for level in levels:
                if skill_matrix_df.loc[werknemer, naam_taak] <= level:
                    ind = skill_index_tuples.index((level, werknemer, taak))
                    skill_eligible[ind] = 1

    # min workers per level per task
    teamsize_index_tuples = list(product(levels, taken))
    min_workers_per_level = np.zeros(len(teamsize_index_tuples))
    for taak in taken:
        for level in levels:
            kolomnaam = f"Aantal_min_niveau_{level}"
            aantal = data_taken.loc[taak, kolomnaam]
            ind = teamsize_index_tuples.index((level, taak))
            min_workers_per_level[ind] = aantal

    # language compatibility parameter
    language_index_tuples = list(product(werknemers, werknemers))
    language_incompatible = np.ones(len(language_index_tuples))
    for werknemer1 in werknemers[:-1]:
        for werknemer2 in werknemers[np.where(werknemers == werknemer1)[0][0]:]:
            if ((present_workers.Nederlands[werknemer1] + present_workers.Nederlands[werknemer2] == 2)
                    or (present_workers.Pools[werknemer1] + present_workers.Pools[werknemer2] == 2)):
                ind = language_index_tuples.index((werknemer1, werknemer2))
                language_incompatible[ind] = 0

    return SolverParams(
        levels=levels,
        taken=taken,
        werknemers=werknemers,
        skill_matrix_df=skill_matrix_df,
        skill_index_tuples=skill_index_tuples,
        skill_eligible=skill_eligible,
        teamsize_index_tuples=teamsize_index_tuples,
        min_workers_per_level=min_workers_per_level,
        language_index_tuples=language_index_tuples,
        language_incompatible=language_incompatible,
    )


def _extract_solution_variables(assignment_vars, skill_index_tuples):
    """Extract assignment variable values from solved model into a DataFrame."""
    raw_solution_df = pd.DataFrame(columns=['x', 'level', 'werknemer', 'taak', 'waarde'])
    count = 0
    for var, (level, worker, task) in zip(assignment_vars, skill_index_tuples):
        if var.varValue is not None:
            raw_solution_df.loc[count, 'x'] = var.name
            raw_solution_df.loc[count, 'level'] = str(level)
            raw_solution_df.loc[count, 'werknemer'] = str(worker)
            raw_solution_df.loc[count, 'taak'] = str(task)
            raw_solution_df.loc[count, 'waarde'] = var.varValue
            count += 1
    return raw_solution_df


def _build_model(params, objective_type, data_taken,
                 include_language=True, skill_level_mode='full'):
    """Build an LP model with configurable constraint groups.

    Parameters
    ----------
    params : SolverParams
    objective_type : str
    data_taken : pd.DataFrame
    include_language : bool - whether to include language compatibility constraints
    skill_level_mode : str - 'full' (level1 min + level3 max), 'partial' (level1 min only), 'none' (skip)

    Returns
    -------
    tuple: (model, assignment_vars, collab_vars, collab_index_tuples)
    """
    levels = params.levels
    taken = params.taken
    werknemers = params.werknemers
    skill_index_tuples = params.skill_index_tuples
    skill_eligible = params.skill_eligible
    teamsize_index_tuples = params.teamsize_index_tuples
    min_workers_per_level = params.min_workers_per_level
    language_index_tuples = params.language_index_tuples
    language_incompatible = params.language_incompatible

    # --- Determine sense ---
    if objective_type == 'Iedereen doet waar hij het beste in is':
        sense = LpMaximize
    else:
        sense = LpMinimize

    model = LpProblem("planning", sense)

    # --- Decision Variables ---
    assignment_vars = [
        LpVariable("x({},{},{})".format(l, w, t), cat=LpBinary)
        for (l, w, t) in product(levels, werknemers, taken)
    ]

    collab_index_tuples = list(product(werknemers, werknemers, taken[data_taken.Samenwerken == 1]))
    collab_vars = [
        LpVariable("t({},{},{})".format(i, j, t), cat=LpBinary)
        for (i, j, t) in collab_index_tuples
    ]

    # --- Objective Functions ---
    if objective_type == 'Iedereen doet waar hij het beste in is':
        model += lpSum([
            assignment_vars[skill_index_tuples.index((1, w, t))]
            + 0.5 * assignment_vars[skill_index_tuples.index((2, w, t))]
            for w in werknemers for t in taken
        ])

    if objective_type == 'Iedereen staat zo veel mogelijk op een machine waar hij nog over moet leren':
        deviation_vars = [
            LpVariable("u({},{})".format(l, t), cat=LpInteger)
            for (l, t) in product(levels, taken)
        ]
        model += lpSum([
            deviation_vars[teamsize_index_tuples.index((l, t))]
            for l in levels for t in taken
        ])
        for level in levels:
            for taak in taken:
                model += (lpSum([
                    assignment_vars[skill_index_tuples.index((level, w, taak))]
                    for w in werknemers
                ]) - min_workers_per_level[teamsize_index_tuples.index((level, taak))] <= deviation_vars[teamsize_index_tuples.index((level, taak))])
                model += (-(lpSum([
                    assignment_vars[skill_index_tuples.index((level, w, taak))]
                    for w in werknemers
                ]) - min_workers_per_level[teamsize_index_tuples.index((level, taak))]) <= deviation_vars[teamsize_index_tuples.index((level, taak))])

    if objective_type == 'Op de belangrijke taken staan goede mensen, op de rest staan beginners':
        deviation_vars = [
            LpVariable("u({},{})".format(l, t), cat=LpInteger)
            for (l, t) in product(levels, taken)
        ]
        model += (
            lpSum([deviation_vars[teamsize_index_tuples.index((l, t))] / 2 for l in levels for t in taken])
            + lpSum([assignment_vars[skill_index_tuples.index((3, w, t))] for w in werknemers for t in taken])
            + lpSum([assignment_vars[skill_index_tuples.index((2, w, t))] / 2 for w in werknemers for t in taken])
        )
        for level in levels:
            for taak in taken:
                model += (lpSum([
                    assignment_vars[skill_index_tuples.index((level, w, taak))]
                    for w in werknemers
                ]) - min_workers_per_level[teamsize_index_tuples.index((level, taak))] <= deviation_vars[teamsize_index_tuples.index((level, taak))])
                model += (-(lpSum([
                    assignment_vars[skill_index_tuples.index((level, w, taak))]
                    for w in werknemers
                ]) - min_workers_per_level[teamsize_index_tuples.index((level, taak))]) <= deviation_vars[teamsize_index_tuples.index((level, taak))])

    # --- Constraints ---

    # CONSTRAINT 1: each worker gets exactly 1 task
    for werknemer in werknemers:
        model += (lpSum([
            assignment_vars[skill_index_tuples.index((l, werknemer, t))]
            for l in levels for t in taken
        ]) == 1)

    # CONSTRAINT 2: a worker is only assigned at a level they actually have
    for level in levels:
        for werknemer in werknemers:
            for taak in taken:
                model += (assignment_vars[skill_index_tuples.index((level, werknemer, taak))]
                          <= skill_eligible[skill_index_tuples.index((level, werknemer, taak))])

    # CONSTRAINT 3: each task gets exactly the required headcount
    for taak in taken:
        aantal_taak = data_taken.loc[taak, 'Aantal']
        model += (lpSum([
            assignment_vars[skill_index_tuples.index((l, w, taak))]
            for l in levels for w in werknemers
        ]) == aantal_taak)

    # CONSTRAINT 4: minimum skill level requirements
    if skill_level_mode in ('full', 'partial'):
        for taak in taken:
            aantal_min_level1 = data_taken.loc[taak, 'Aantal_min_niveau_1']
            model += (lpSum([assignment_vars[skill_index_tuples.index((1, w, taak))] for w in werknemers]) >= aantal_min_level1)
        if skill_level_mode == 'full':
            for taak in taken:
                aantal_max_level3 = data_taken.loc[taak, 'Aantal_min_niveau_3']
                model += (lpSum([assignment_vars[skill_index_tuples.index((3, w, taak))] for w in werknemers]) <= aantal_max_level3)

    # CONSTRAINT 5: language compatibility
    if include_language:
        for worker_i in werknemers:
            ind = werknemers.tolist().index(worker_i)
            for worker_j in werknemers[ind + 1:]:
                for taak in taken[data_taken.Samenwerken == 1]:
                    model += (lpSum([assignment_vars[skill_index_tuples.index((l, worker_i, taak))] for l in levels]) >= collab_vars[collab_index_tuples.index((worker_i, worker_j, taak))])
                    model += (lpSum([assignment_vars[skill_index_tuples.index((l, worker_j, taak))] for l in levels]) >= collab_vars[collab_index_tuples.index((worker_i, worker_j, taak))])
                    model += (lpSum([
                        assignment_vars[skill_index_tuples.index((l, worker_i, taak))]
                        + assignment_vars[skill_index_tuples.index((l, worker_j, taak))]
                        for l in levels
                    ]) - 1 <= collab_vars[collab_index_tuples.index((worker_i, worker_j, taak))])

        for taak in taken[data_taken.Samenwerken == 1]:
            model += (lpSum([
                collab_vars[collab_index_tuples.index((worker_i, worker_j, taak))]
                * language_incompatible[language_index_tuples.index((worker_i, worker_j))]
                for worker_i in werknemers for worker_j in werknemers
            ]) == 0)

    return model, assignment_vars, collab_vars, collab_index_tuples


def build_and_solve(params, objective_type, data_taken, present_workers):
    """Create LP model, solve, and return SolveResult.

    Parameters
    ----------
    params : SolverParams
    objective_type : str - one of the three Dutch objective function descriptions
    data_taken : pd.DataFrame - filtered tasks DataFrame (Aan==1)
    present_workers : pd.DataFrame - present workers DataFrame (for name lookup)

    Returns
    -------
    SolveResult
    """
    levels = params.levels
    taken = params.taken
    werknemers = params.werknemers
    skill_index_tuples = params.skill_index_tuples
    skill_eligible = params.skill_eligible

    raw_solution_df = pd.DataFrame(columns=['x', 'level', 'werknemer', 'taak', 'waarde'])
    warnings = []
    solver = PULP_CBC_CMD(msg=0, timeLimit=300)

    # --- Relaxation attempts ---
    relaxation_configs = [
        # attempt 0: full model
        dict(include_language=True,  skill_level_mode='full'),
        # attempt 1: remove language
        dict(include_language=False, skill_level_mode='full'),
        # attempt 2: remove level 3 max (partial skill)
        dict(include_language=True,  skill_level_mode='partial'),
        # attempt 3: remove all skill levels
        dict(include_language=True,  skill_level_mode='none'),
        # attempt 4: remove skill levels + language
        dict(include_language=False, skill_level_mode='none'),
    ]

    for attempt, config in enumerate(relaxation_configs):
        model, assignment_vars, _, _ = _build_model(
            params, objective_type, data_taken, **config)
        model.solve(solver)

        if model.status == 1:  # OPTIMAL
            raw_solution_df = _extract_solution_variables(assignment_vars, skill_index_tuples)

            if attempt == 0:
                return SolveResult(
                    status='optimal',
                    raw_solution_df=raw_solution_df,
                )

            if attempt == 1:
                warnings.append('LET OP! De planning voldoet niet aan de volgende eis:\n\n* Werknemers spreken niet overal dezelfde taal, waar nodig')
            if attempt == 2:
                warnings.append('LET OP! De planning voldoet niet aan de volgende eis:\n\n* Op de taken zijn er voldoende werknemers op niveau 1 ingedeeld, maar er wordt niet voldaan aan de minimum eisen van niveau 2 en 3')
            if attempt == 3:
                warnings.append('LET OP! De planning voldoet niet aan de volgende eis:\n\n* Er wordt niet aan de minimum eisen van de niveaus van de taken voldaan')
            if attempt == 4:
                warnings.append('LET OP! De planning voldoet niet aan de volgende eisen: \n\n* Er wordt niet aan de minimum eisen van de niveaus van de taken voldaan \n\n* Werknemers spreken niet overal dezelfde taal, waar nodig')

            return SolveResult(
                status='relaxed',
                raw_solution_df=raw_solution_df,
                relaxation_attempt=attempt,
                warnings=warnings,
            )

        if model.status != -1:  # not INFEASIBLE — some other status
            return SolveResult(
                status='other',
                raw_solution_df=raw_solution_df,
                warnings=['Model status is niet optimaal, maar ook niet infeasible'],
            )

        # INFEASIBLE — continue to next relaxation attempt

    # All attempts exhausted — diagnose
    mensen_alleen_4 = []
    for werknemer in werknemers:
        count = 0
        for level in levels:
            for taak in taken:
                count += skill_eligible[skill_index_tuples.index((level, werknemer, taak))]
        if count == 0:
            mensen_alleen_4.append(present_workers.loc[werknemer, 'Werknemers'])

    taken_alleen_4 = []
    for taak in taken:
        count = 0
        for level in levels:
            for werknemer in werknemers:
                count += skill_eligible[skill_index_tuples.index((level, werknemer, taak))]
        if count == 0:
            taken_alleen_4.append(data_taken.loc[taak, 'Taken'])

    if len(mensen_alleen_4) == 1:
        warnings.append(f'{mensen_alleen_4[0]} bezit op de huidige taken alleen niveau 4, diegene kan dus nergens ingepland worden en daardoor loopt de planning fout. Selecteer een andere taak, een andere medewerker of pas het competentieniveau van deze persoon aan.')
    if len(mensen_alleen_4) > 1:
        warnings.append(' en '.join(str(w) for w in mensen_alleen_4) + ' bezitten alleen niveau 4 op de huidige taken. Zij kunnen dus niet worden ingepland en daardoor loopt de planning fout. Selecteer andere taken, anderen medewerkers of pas de competentieniveaus van deze personen aan.')

    if len(taken_alleen_4) == 1:
        warnings.append(f'Voor de taak {taken_alleen_4[0]} is niemand aanwezig die niet niveau 4 heeft. Daardoor kan er niemand worden ingepland. Zorg ervoor dat er een werknemer met een ander niveau aanwezig is, of dat de taak niet wordt uitgevoerd.')
    if len(taken_alleen_4) > 1:
        warnings.append('Voor de taken ' + ' en '.join(str(t) for t in taken_alleen_4) + ' zijn er geen werknemers aanwezig die geen niveau 4 hebben. Daardoor kan er niemand worden ingepland. Zorg ervoor dat er werknemers met een ander niveau aanwezig zijn, of dat de taken niet worden uitgevoerd.')

    return SolveResult(
        status='infeasible',
        raw_solution_df=raw_solution_df,
        relaxation_attempt=len(relaxation_configs),
        warnings=warnings,
        level4_only_workers=mensen_alleen_4,
        level4_only_tasks=taken_alleen_4,
    )
