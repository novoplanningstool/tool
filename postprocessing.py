"""Solution post-processing: decoding, board layout, absent workers."""

import pandas as pd
from collections import defaultdict


def decode_solution(raw_solution_df, present_workers, data_taken):
    """Filter to assigned variables (waarde==1), replace numeric indices with names.

    Returns a task->workers DataFrame built from the solver solution.
    """
    filtered_solution_df = raw_solution_df.loc[
        raw_solution_df['waarde'] == 1, ['werknemer', 'taak', 'level']
    ]
    for mens in filtered_solution_df.index:
        naam_ind = int(filtered_solution_df.loc[mens, 'werknemer'])
        naam = present_workers.loc[naam_ind, 'Werknemers']
        filtered_solution_df.loc[mens, 'werknemer'] = naam

        taak_ind = int(filtered_solution_df.loc[mens, 'taak'])
        taak = data_taken.loc[taak_ind, 'Taken']
        filtered_solution_df.loc[mens, 'taak'] = taak

    task_assignments = defaultdict(list)
    for taak in data_taken.Taken:
        task_assignments[taak] = []

    for i in filtered_solution_df.index:
        taak = filtered_solution_df.loc[i, 'taak']
        werknemer = filtered_solution_df.loc[i, 'werknemer']
        task_assignments[taak].append(werknemer)

    return pd.DataFrame.from_dict(task_assignments, orient='index')


def build_full_planning(extra_task_dfs, solution_df):
    """Concatenate special task DataFrames with the solver solution."""
    return pd.concat(extra_task_dfs + [solution_df])


def split_boards(full_planning_df, data_tasks, onetime_tasks_df):
    """Split tasks into left/right board based on 'Verdeling oud planbord' column.

    Returns (left_board_df, right_board_df).
    """
    alle_taken = ['Laden/lossen Zeelandia'] + list(data_tasks['Taken'])
    verdeling = [1] + list(data_tasks['Verdeling oud planbord'])

    left_board_df = pd.DataFrame()
    right_board_df = pd.DataFrame()

    for taak in full_planning_df.index:
        ind = list(alle_taken).index(taak)
        kant = verdeling[ind]
        if kant == 1:
            left_board_df = pd.concat([left_board_df, full_planning_df.loc[taak, :]], axis=1)
        elif kant == 2:
            right_board_df = pd.concat([right_board_df, full_planning_df.loc[taak, :]], axis=1)

    left_board_df = left_board_df.T
    right_board_df = right_board_df.T

    left_board_df = pd.concat([left_board_df, onetime_tasks_df])

    left_board_df = left_board_df.fillna('')
    right_board_df = right_board_df.fillna('')

    return left_board_df, right_board_df


def compute_absent_workers(data_werknemers, mensen_aanwezig_niet_in_planning):
    """Return DataFrame of absent workers (excluding those in special tasks)."""
    afwezig = data_werknemers[data_werknemers['Aanwezig'] == 0].Werknemers
    afwezig = set(afwezig) - set(mensen_aanwezig_niet_in_planning)
    return pd.DataFrame(afwezig)


def rename_board_columns(board_df):
    """Rename columns to 'Werknemer 1', 'Werknemer 2', etc."""
    colnames = [f"Werknemer {i+1}" for i in range(len(board_df.columns))]
    board_df.columns = colnames
    return board_df
