"""Data loading, validation, and temp worker utilities."""

import datetime
import pandas as pd
from collections import defaultdict


def validate_task_columns(werknemers_df, taken_df, uitzendkracht_df):
    """Return set of mismatched column names between the three sheets.

    An empty set means all task names are consistent across sheets.
    """
    col1 = set(werknemers_df.columns)
    col2 = set(taken_df.Taken)
    col3 = set(uitzendkracht_df.columns)
    for i in ['Werknemers', 'Aanwezig', 'Pools', 'Nederlands', 'Vrije dagen']:
        col1.remove(i)
        col3.remove(i)

    return (col1 | col2 | col3) - (col1 & col2 & col3)


def compute_default_day():
    """Return index into the days list based on current weekday."""
    vandaag_i = datetime.datetime.today().weekday()
    if vandaag_i in [0, 1, 2, 3]:
        return vandaag_i + 1
    return 0


def get_employees_with_day_off(employees_df, day):
    """Return list of employee names whose 'Vrije dagen' includes the given day."""
    result = []
    day_lower = day.strip().lower()
    for i in range(len(employees_df)):
        raw_value = employees_df.loc[i, "Vrije dagen"]
        if pd.isna(raw_value):
            continue
        days_off = [d.strip().lower() for d in str(raw_value).replace(",", " ").split()]
        if day_lower in days_off:
            result.append(employees_df.loc[i, "Werknemers"])
    return result


def add_temp_workers(werknemers_df, uitzendkracht_df, count, start_index=1):
    """Append `count` temp worker rows to werknemers_df using the uitzendkracht template.

    Returns a new DataFrame with the temp workers appended.
    """
    result = werknemers_df
    for i in range(int(count)):
        uitzendkracht_skills = [f"Uitzendkracht {start_index + i}"] + list(
            uitzendkracht_df.drop(columns=['Werknemers']).iloc[0]
        )
        df_uitzendkracht_skills = pd.DataFrame(uitzendkracht_skills).transpose()
        df_uitzendkracht_skills.columns = result.columns
        result = pd.concat([result, df_uitzendkracht_skills], ignore_index=True)
    return result


def build_task_worker_map(tasks_and_workers_df):
    """Convert a 2-column DataFrame (col 0=task, col 1=worker) into a task->workers DataFrame.

    Returns a DataFrame with tasks as index and workers spread across columns.
    """
    task_worker_map = defaultdict(list)
    for taak in tasks_and_workers_df[0]:
        task_worker_map[taak] = []

    for i in tasks_and_workers_df.index:
        taak = tasks_and_workers_df[0][i]
        werknemer = tasks_and_workers_df[1][i]
        task_worker_map[taak].append(werknemer)

    return pd.DataFrame.from_dict(task_worker_map, orient='index')


def process_remaining_skill_levels(data_taken):
    """Distribute 'rest' workers to the appropriate Aantal_min_niveau_X column.

    Modifies data_taken in place.
    """
    for i in data_taken.index:
        ingepland = (data_taken.loc[i, 'Aantal_min_niveau_2']
                     + data_taken.loc[i, 'Aantal_min_niveau_1']
                     + data_taken.loc[i, 'Aantal_min_niveau_3'])
        if data_taken.loc[i, 'Aantal'] > ingepland:
            niet_ingedeeld = data_taken.loc[i, 'Aantal'] - ingepland
            data_taken.loc[i, f"Aantal_min_niveau_{int(data_taken.loc[i, 'Rest_min_niveau'])}"] += niet_ingedeeld
