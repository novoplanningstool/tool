# -*- coding: utf-8 -*-
"""
Created on Thu Apr 21 12:10:01 2022

@author: Émile
"""
import pandas as pd
import streamlit as st

from data_loading import (
    validate_task_columns,
    compute_default_day,
    get_employees_with_day_off,
    add_temp_workers,
    build_task_worker_map,
    process_remaining_skill_levels,
)
from solver import build_solver_parameters, build_and_solve
from postprocessing import (
    decode_solution,
    build_full_planning,
    split_boards,
    compute_absent_workers,
    rename_board_columns,
)
from excel_export import generate_excel

# --- Data Upload & Validation ---

st.image("https://raw.githubusercontent.com/NovoPW/Planningstool/main/NOVO-Logo.png")
st.write("""
         ***

         # Planning Generator

         Deze tool is gemaakt voor het genereren van een planning voor de productieafdeling van NOVO Packaging & Warehousing. Probeer zo veel mogelijk het originele databestand te gebruiken. Verander dus zo min mogelijk kolom- en rijnamen, met uitzondering van het toevoegen van extra personeel en taken.

         ***
         """)

st.markdown("#")

uploaded_file = st.file_uploader("Selecteer hier het databestand waarin de gegevens over de werknemers en machines staan.")
if uploaded_file is not None:
    werknemersDataFrame = pd.read_excel(uploaded_file, sheet_name='Werknemers')
    takenDataFrame = pd.read_excel(uploaded_file, sheet_name='Taken')
    uitzendKrachtDataFrame = pd.read_excel(uploaded_file, sheet_name='Uitzendkracht')
    extra_task_dfs = []

    mismatched_task_columns = validate_task_columns(werknemersDataFrame, takenDataFrame, uitzendKrachtDataFrame)
    if len(mismatched_task_columns) > 0:
        st.error(f'Er gaat iets fout met de taken. Denk eraan dat alle taken ook als kolom terug moeten komen bij de werknemers EN de uitzendkracht in de data. Ook als een taak verwijderd wordt moet de bijbehorende kolom verwijderd worden. Het gaat om de volgende namen:{list(mismatched_task_columns)}')

    new_werknemersDataFrame = werknemersDataFrame
    st.markdown("#")

    # --- Day Selection ---

    dagen = ['maandag', 'dinsdag', 'woensdag', 'donderdag','vrijdag', 'zaterdag', 'zondag']
    defaultdag = compute_default_day()
    dag = st.selectbox('Voor wanneer is de planning?',dagen,index=defaultdag)

    st.markdown("#")
    st.write("""
             ***
             ### Werknemers informatie
             ***
             """)

    # --- Employee Attendance ---

    # Manage attendance selection entirely via session state so that
    # changing the day automatically removes employees with a day off.
    employees_with_day_off = get_employees_with_day_off(new_werknemersDataFrame, dag)

    if 'previous_dag' not in st.session_state:
        st.session_state.previous_dag = dag
    if 'aanwezigen_select' not in st.session_state:
        st.session_state.aanwezigen_select = [
            name for name in new_werknemersDataFrame["Werknemers"]
            if name not in employees_with_day_off
        ]
    if dag != st.session_state.previous_dag:
        previous_day_off = get_employees_with_day_off(new_werknemersDataFrame, st.session_state.previous_dag)
        # Restore employees who had the previous day off but not the new day off
        to_restore = [name for name in previous_day_off if name not in employees_with_day_off]
        # Remove employees who have the new day off
        updated = [
            name for name in st.session_state.aanwezigen_select
            if name not in employees_with_day_off
        ] + to_restore
        # Preserve original order
        all_names = list(new_werknemersDataFrame["Werknemers"])
        st.session_state.aanwezigen_select = [name for name in all_names if name in updated]
        st.session_state.previous_dag = dag

    mensen_op_de_werkvloer = []
    if st.checkbox("Zet iedereen op afwezig"):
        st.session_state.aanwezigen_select = []
    aanwezigen = st.multiselect(
         'Wie zijn er aanwezig?',new_werknemersDataFrame['Werknemers'],
         key='aanwezigen_select')
    for i in range(len(new_werknemersDataFrame["Werknemers"])):
      if new_werknemersDataFrame.loc[i, "Werknemers"] in aanwezigen:
          new_werknemersDataFrame.loc[i, "Aanwezig"] = 1
          mensen_op_de_werkvloer.append(new_werknemersDataFrame.loc[i, "Werknemers"])
      else:
          new_werknemersDataFrame.loc[i, "Aanwezig"] = 0

    st.text("")

    # --- Temp Workers ---

    if st.checkbox("Wil je zelf het aantal uitzendkrachten opgeven?"):
        uitzendkrachten = st.number_input("Hoeveel uitzendkrachten zijn er?",min_value=0, value = 1, step = 1)
        new_werknemersDataFrame = add_temp_workers(new_werknemersDataFrame, uitzendKrachtDataFrame, uitzendkrachten)
    else:
        uitzendkrachten = 0
    st.text("")

    st.markdown("#")
    st.markdown("#")

    # --- Task Configuration ---

    st.write("""
             ***
             ### Taken informatie
             ***

             """)

    st.text("")

    new_takenDataFrame = takenDataFrame

    # --- One-Off Tasks ---

    onetime_tasks_df = pd.DataFrame()
    mensen_aanwezig_niet_in_planning = []
    if st.checkbox('Zijn er eenmalige taken die niet in de data omschreven zijn?'):
        aantal_taken = st.number_input("Hoeveel taken wil je toevoegen?",min_value=1, value = 1, step = 1)
        speciale_mensen_list = []
        speciale_taken_list = []
        for i in range(int(aantal_taken)):
            speciale_taak = st.text_input(f"Hoe heet deze {i+1}e taak die toegevoegd moet worden?")
            speciale_mense = st.multiselect(
                f"Wie gaan deze {i+1}e taak uitvoeren?",
                new_werknemersDataFrame['Werknemers'][new_werknemersDataFrame["Aanwezig"]==1])
            for i in range(3):
                st.write(" ")
            speciale_mensen_list = speciale_mensen_list + speciale_mense
            for speciaal_mens in speciale_mense:
                speciale_taken_list.append(speciale_taak)
                mensen_aanwezig_niet_in_planning.append(speciaal_mens)
                new_werknemersDataFrame.loc[new_werknemersDataFrame["Werknemers"]==speciaal_mens, "Aanwezig"] = 0
        df_speciale_taken_mensen = pd.DataFrame([speciale_taken_list,speciale_mensen_list]).T
        onetime_tasks_df = build_task_worker_map(df_speciale_taken_mensen)

    st.markdown("#")
    st.markdown("#")

    vooraf_aanwezig = list(new_takenDataFrame["Taken"])
    if st.checkbox("Zet alle taken uit"):
        vooraf_aanwezig = list(new_takenDataFrame["Taken"][new_takenDataFrame["Aan"]==1])
    aanwezige_taken = st.multiselect(
         'Welke taken moeten er gedaan worden?',new_takenDataFrame['Taken'],vooraf_aanwezig)
    for i in range(len(new_takenDataFrame["Taken"])):
      if new_takenDataFrame.loc[i, "Taken"] in aanwezige_taken:
          new_takenDataFrame.loc[i, "Aan"] = 1
      else:
          new_takenDataFrame.loc[i, "Aan"] = 0

    st.markdown("#")

    st.write("Hoeveel mensen zijn er nodig op elke taak?")

    grid_edited_tasks = st.data_editor(new_takenDataFrame[['Taken','Aantal']][new_takenDataFrame['Aan']==1], disabled=['Taken'], width="stretch")
    for i in new_takenDataFrame['Taken'][new_takenDataFrame['Aan']==1]:
        new_takenDataFrame.loc[new_takenDataFrame['Taken']==i,'Aantal'] = int(grid_edited_tasks.loc[grid_edited_tasks['Taken']==i, 'Aantal'].iloc[0])

    if len(new_werknemersDataFrame[new_werknemersDataFrame['Aanwezig'] == 1]) > sum(new_takenDataFrame[new_takenDataFrame['Aan']==1]['Aantal']):
        aantal_werknemers = len(new_werknemersDataFrame[new_werknemersDataFrame['Aanwezig'] == 1])
        aantal_benodigd_voor_taken = sum(new_takenDataFrame[new_takenDataFrame['Aan']==1]['Aantal'])

        st.write(f'LET OP: Het aantal werknemers is niet gelijk aan het benodigde aantal werknemers voor alle taken. Er zijn {aantal_werknemers} werknemers aanwezig en er zijn {aantal_benodigd_voor_taken} werknemers nodig om alle taken af te kunnen ronden.')

    st.markdown("#")
    st.markdown("#")

    # --- Pin Workers to Tasks ---

    if st.checkbox('Zijn er mensen die per se een bepaalde taak moeten afronden?'):
        aantal_taken = st.number_input("Van hoeveel taken wil je vooraf de mensen opgeven?",min_value=1, value = 1, step = 1)
        speciale_mensen_list = []
        speciale_taken_list = []
        for i in range(int(aantal_taken)):
            speciale_taak = st.selectbox(f"Wat is de {i+1}e taak?",new_takenDataFrame["Taken"][new_takenDataFrame["Aan"]==1])
            new_takenDataFrame.loc[new_takenDataFrame["Taken"]==speciale_taak, "Aan"] = 0
            speciale_mensen = st.multiselect(
                 f"Wie gaan deze {i+1}e taak uitvoeren?",new_werknemersDataFrame['Werknemers'][new_werknemersDataFrame["Aanwezig"]==1])
            if len(speciale_mensen)==int(new_takenDataFrame.loc[new_takenDataFrame["Taken"]==speciale_taak, "Aantal"].iloc[0]):
                st.write("Je hebt het juiste aantal mensen geselecteerd voor deze taak!")
            else:
                st.write("Je moet nog ",int(new_takenDataFrame.loc[new_takenDataFrame["Taken"]==speciale_taak, "Aantal"].iloc[0])-len(speciale_mensen)," werknemers kiezen voor deze taak." )
            for i in range(3):
                st.write(" ")
            speciale_mensen_list = speciale_mensen_list + speciale_mensen
            for speciaal_mens in speciale_mensen:
                speciale_taken_list.append(speciale_taak)
                mensen_aanwezig_niet_in_planning.append(speciaal_mens)
                new_werknemersDataFrame.loc[new_werknemersDataFrame["Werknemers"]==speciaal_mens, "Aanwezig"] = 0
        df_speciale_taken_mensen = pd.DataFrame([speciale_taken_list,speciale_mensen_list]).T
        pinned_tasks_df = build_task_worker_map(df_speciale_taken_mensen)
        extra_task_dfs.append(pinned_tasks_df)

    # --- Zeelandia Loading/Unloading ---

    st.markdown("#")
    if st.checkbox('Staat laden/lossen voor Zeelandia vandaag op de planning?',value=True):
        zeelandia = st.multiselect(
            'Wie gaat laden/lossen?',mensen_op_de_werkvloer)

        zeelandia_df = pd.DataFrame.from_dict({'Laden/lossen Zeelandia': zeelandia}, orient = 'index')
        extra_task_dfs = [zeelandia_df]+extra_task_dfs

    # --- Objective Function Selection ---

    st.markdown("#")

    st.write("""
             ***
             ### Planning maken en opslaan
             ***
             """)

    st.text("")
    doelfunctie_keuzebox = st.selectbox(
         'Hoe moet de planning eruit zien?',
         ('Iedereen doet waar hij het beste in is', 'Iedereen staat zo veel mogelijk op een machine waar hij nog over moet leren', 'Op de belangrijke taken staan goede mensen, op de rest staan beginners'))

    st.markdown("#")

    if st.checkbox('Planning genereren'):
        if len(new_werknemersDataFrame[new_werknemersDataFrame['Aanwezig'] == 1]) > sum(new_takenDataFrame[new_takenDataFrame['Aan']==1]['Aantal']):
            aantal_werknemers = len(new_werknemersDataFrame[new_werknemersDataFrame['Aanwezig'] == 1])
            aantal_benodigd_voor_taken = sum(new_takenDataFrame[new_takenDataFrame['Aan']==1]['Aantal'])
            st.error(f'LET OP: Het aantal werknemers is niet gelijk aan het benodigde aantal werknemers voor alle taken. Er zijn {aantal_werknemers} werknemers aanwezig en er zijn {aantal_benodigd_voor_taken} werknemers nodig om alle taken af te kunnen ronden.')
        else:
            # uitzendkrachten automatisch aanvullen
            if len(new_werknemersDataFrame[new_werknemersDataFrame['Aanwezig'] == 1]) < sum(new_takenDataFrame[new_takenDataFrame['Aan']==1]['Aantal']):
                aantal_benodigde_uitzendkrachten = sum(new_takenDataFrame[new_takenDataFrame['Aan']==1]['Aantal']) - len(new_werknemersDataFrame[new_werknemersDataFrame['Aanwezig'] == 1])
                st.warning(f'WARNING: Het aantal werknemers is kleiner dan het benodigde aantal werknemers voor alle taken. Er zijn {aantal_benodigde_uitzendkrachten} uitzendkrachten toegevoegd om te voldoen aan de hoeveelheid taken.')
                new_werknemersDataFrame = add_temp_workers(
                    new_werknemersDataFrame, uitzendKrachtDataFrame,
                    sum(new_takenDataFrame[new_takenDataFrame['Aan']==1]['Aantal']) - len(new_werknemersDataFrame[new_werknemersDataFrame['Aanwezig'] == 1])
                )
            data_werknemers = new_werknemersDataFrame
            data_tasks = new_takenDataFrame

            # --- MIP Model: Data Preparation ---

            present_workers = data_werknemers.loc[data_werknemers['Aanwezig'] == 1,:]
            data_taken = data_tasks.loc[data_tasks['Aan']==1,:]

            process_remaining_skill_levels(data_taken)

            params = build_solver_parameters(present_workers, data_taken)
            result = build_and_solve(params, doelfunctie_keuzebox, data_taken, present_workers)

            # Display warnings from solver
            for warning_msg in result.warnings:
                st.warning(warning_msg)

            if result.status == 'infeasible':
                st.write('Er kan helaas geen oplossing gevonden worden.')
            elif result.status == 'other':
                st.write('Model status is niet optimaal, maar ook niet infeasible')
            else:
                # --- Solution Post-Processing ---
                solution_df = decode_solution(result.raw_solution_df, present_workers, data_taken)
                full_planning_df = build_full_planning(extra_task_dfs, solution_df)
                left_board_df, right_board_df = split_boards(full_planning_df, data_tasks, onetime_tasks_df)
                full_planning_df = full_planning_df.fillna('')
                afwezig = compute_absent_workers(data_werknemers, mensen_aanwezig_niet_in_planning)

                # --- Display & Excel Export ---
                left_board_df = rename_board_columns(left_board_df)
                right_board_df = rename_board_columns(right_board_df)

                display_df = pd.concat([left_board_df, right_board_df])
                display_df = display_df.fillna('')
                st.dataframe(display_df)

                bestandsnaam = 'Dagplanning.xlsx'
                df_xlsx = generate_excel(left_board_df, right_board_df, full_planning_df, afwezig, dag)
                st.download_button(label='📥 Download planning als Excel bestand',
                                                data=df_xlsx ,
                                                file_name= bestandsnaam)

# leegte om de disclaimer onderaan de pagina te krijgen
for i in range(20):
    st.text("")

st.write("""
         ###### Disclaimer
         Deze tool is gemaakt door Rachelle Hermans en Emile Baljeu. Zij zijn, evenals Fontys Hogescholen, niet aansprakelijk voor mogelijke complicaties tijdens en/of na het gebruik van deze site. Ook hebben zij geen rechten voor het gebruiken van het logo op deze site, dus klaag ze alstublieft niet aan. Groetjes!""")
