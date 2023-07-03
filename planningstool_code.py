# -*- coding: utf-8 -*-
"""
Created on Thu Apr 21 12:10:01 2022

@author: Émile
"""
import datetime
import numpy as np
import pandas as pd
import streamlit as st

from io import BytesIO
from st_aggrid import AgGrid
from itertools import product
from collections import defaultdict
from mip import Model, xsum, minimize, maximize, BINARY, CBC, OptimizationStatus, INTEGER
from urllib.request import urlopen

#eventueel de paginabreedte wijder maken:
#st.set_page_config(layout="wide")

#het logo op de voorkant
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
    dataframe = pd.read_excel(uploaded_file, sheet_name='Werknemers')
    dataframe2 = pd.read_excel(uploaded_file, sheet_name='Taken')
    dataframe3 = pd.read_excel(uploaded_file, sheet_name='Uitzendkracht')
    df_pre_concat_list = []
    
    col1 = set(dataframe.columns)
    col2 = set(dataframe2.Taken)
    col3 = set(dataframe3.columns)
    for i in ['Werknemers','Aanwezig','Pools','Nederlands','Vrije dagen']:
        col1.remove(i)
        col3.remove(i)
    
    
    a = (col1 | col2 | col3) - (col1 & col2 & col3)
    if len(a) > 0:
        st.error(''.join(['Er gaat iets fout met de taken. Denk eraan dat alle taken ook als kolom terug moeten komen bij de werknemers EN de uitzendkracht in de data. Ook als een taak verwijderd wordt moet de bijbehorende kolom verwijderd worden. Het gaat om de volgende namen:' , str(list(a)[:])]))
    
    
    
    
    
    
    
    
    new_df = dataframe
    st.markdown("#")  
    
# =============================================================================
#     dagen = ['maandag', 'dinsdag', 'woensdag', 'donderdag','vrijdag', 'zaterdag', 'zondag']
#     vandaag_i = datetime.datetime.today().weekday()
#     dagen_gesorteerd = dagen[vandaag_i:7]+dagen[0:vandaag_i]
#     dag = st.selectbox(
#          'Voor wanneer is de planning?',dagen_gesorteerd)
# =============================================================================
    
    dagen = ['maandag', 'dinsdag', 'woensdag', 'donderdag','vrijdag', 'zaterdag', 'zondag']
    vandaag_i = datetime.datetime.today().weekday()
    if vandaag_i in [0, 1, 2, 3]:
        defaultdag = vandaag_i+1
    else:
        defaultdag = 0
    #dagen_gesorteerd = dagen[vandaag_i:7]+dagen[0:vandaag_i]
    dag = st.selectbox('Voor wanneer is de planning?',dagen,index=defaultdag)

    st.markdown("#")
    st.write("""
             ***
             ### Werknemers informatie
             ***
             """)
    
# =============================================================================
#     vrije_mensen = []
#     for i in range(len(new_df["Vrije dagen"])):
#         if str(new_df["Vrije dagen"][i]) != 'nan':
#             vrije_dagen = new_df["Vrije dagen"][i].lower().split()
#             if dag in vrije_dagen:
#                 vrije_mensen.append(new_df["Werknemers"][i])
#     
# =============================================================================
# =============================================================================
#     if st.checkbox("Ik heb de afwezigen al opgegeven in het ingeladen excel-bestand."):
#         vrije_mensen = list(new_df["Werknemers"][new_df["Aanwezig"]==0])
# =============================================================================
# =============================================================================
#     mensen_op_de_werkvloer = []
#     if st.checkbox("Zet iedereen op afwezig"):
#         vrije_mensen = list(new_df["Werknemers"])
#     afwezigen = st.multiselect(
#          'Wie zijn er afwezig?',new_df['Werknemers'],vrije_mensen)
#     #afwezigen verwerken:
#     for i in range(len(new_df["Werknemers"])):
#         if new_df["Werknemers"][i] in afwezigen:
#             new_df["Aanwezig"][i] = 0
#         else:
#             new_df["Aanwezig"][i] = 1
#             mensen_op_de_werkvloer.append(new_df["Werknemers"][i])
# =============================================================================
    
    mensen_op_de_werkvloer = []
    if st.checkbox("Zet iedereen op afwezig"):
        default_aanwezigen = []
    else:
        default_aanwezigen = list(new_df["Werknemers"])
    aanwezigen = st.multiselect(
         'Wie zijn er aanwezig?',new_df['Werknemers'],default_aanwezigen)
    #aanwezigen verwerken:
    for i in range(len(new_df["Werknemers"])):
        if new_df["Werknemers"][i] in aanwezigen:
            new_df["Aanwezig"][i] = 1
            mensen_op_de_werkvloer.append(new_df["Werknemers"][i])
        else:
            new_df["Aanwezig"][i] = 0
            
    
    
    
    
    
    
    
    st.text("")
    
    if st.checkbox("Wil je zelf het aantal uitzendkrachten opgeven?"):
        uitzendkrachten = st.number_input("Hoeveel uitzendkrachten zijn er?",min_value=0, value = 1, step = 1)
        for i in range(int(uitzendkrachten)):
            uitzendkracht_skills = ["".join(["Uitzendkracht ",str(i+1)])] + list(dataframe3.loc[0,dataframe3.columns!="Werknemers"])
            df_uitzendkracht_skills = pd.DataFrame(uitzendkracht_skills).transpose()
            df_uitzendkracht_skills.columns = new_df.columns
            new_df = pd.concat([new_df, df_uitzendkracht_skills], ignore_index = True)
    else:
        uitzendkrachten = 0
    st.text("")
    
    st.markdown("#")
    st.markdown("#")
    
    
    st.write("""
             ***
             ### Taken informatie
             ***
             
             """)
    
    st.text("")
             
    
# =============================================================================
#     if st.checkbox('Ruwe taken-data bewerken'):
#         #grid_return = AgGrid(dataframe, editable=True)
#         #new_df = grid_return['data']
# 
#         grid_return2 = AgGrid(dataframe2, editable=True)
#         new_df2 = grid_return2['data']
#     else:
#         #new_df = dataframe
# =============================================================================
    new_df2 = dataframe2
    
    
    
    # eenmalige taak toevoegen
    df_eenmalig = pd.DataFrame()
    mensen_aanwezig_niet_in_planning = []
    if st.checkbox('Zijn er eenmalige taken die niet in de data omschreven zijn?'):
        aantal_taken = st.number_input("Hoeveel taken wil je toevoegen?",min_value=1, value = 1, step = 1)
        speciale_mensen_list = []
        #speciale_mensen_niveau_list = []
        speciale_taken_list = []
        for i in range(int(aantal_taken)):
            speciale_taak = st.text_input("".join(["Hoe heet deze ",str(i+1),"e taak die toegevoegd moet worden?"]))
            speciale_mense = st.multiselect(
                "".join(["Wie gaan deze ",str(i+1),"e taak uitvoeren?"]),
                new_df['Werknemers'][new_df["Aanwezig"]==1])
            for i in range(3):
                st.write(" ")
            speciale_mensen_list = speciale_mensen_list + speciale_mense
            for speciaal_mens in speciale_mense:
                speciale_taken_list.append(speciale_taak)
                #speciale_mensen_niveau_list.append(new_df[speciale_taak][speciaal_mens])
                mensen_aanwezig_niet_in_planning.append(speciaal_mens)
                new_df["Aanwezig"][new_df["Werknemers"]==speciaal_mens]=0
        df_speciale_taken_mensen = pd.DataFrame([speciale_taken_list,speciale_mensen_list]).T   
        
        #maximum op opgegeven bovengrens capaciteit taak? #!!!!!
        # lege dictionary maken, met de taken als keys
        oplossing1 = defaultdict(list)
        for taak in df_speciale_taken_mensen[0]:
            oplossing1[taak]=[]

        # Werknemers toevoegen aan de values van de keys (taken)
        for i in df_speciale_taken_mensen.index:
            taak = df_speciale_taken_mensen[0][i]
            werknemer = df_speciale_taken_mensen[1][i]
            oplossing1[taak].append(werknemer)
        
        # terug naar een dataframe
        df_eenmalig = pd.DataFrame.from_dict(oplossing1, orient = 'index')
        #df_pre_concat_list.append(df2)
    
    
    
    st.markdown("#")
    st.markdown("#")    
    

    
    vooraf_aanwezig = list(new_df2["Taken"])
    if st.checkbox("Zet alle taken uit"):
        vooraf_aanwezig = list(new_df2["Taken"][new_df2["Aan"]==1])
    aanwezige_taken = st.multiselect(
         'Welke taken moeten er gedaan worden?',new_df2['Taken'],vooraf_aanwezig)
    #aanwezige taken verwerken:
    for i in range(len(new_df2["Taken"])):
        if new_df2["Taken"][i] in aanwezige_taken:
            new_df2["Aan"][i] = 1
        else:
            new_df2["Aan"][i] = 0
    
    st.markdown("#")
    
    st.write("Hoeveel mensen zijn er nodig op elke taak?")

    grid_return4 = AgGrid(new_df2[['Taken','Aantal']][new_df2['Aan']==1], editable=True, fit_columns_on_grid_load=True)
    temp = grid_return4['data']
    for i in new_df2['Taken'][new_df2['Aan']==1]:
        new_df2.loc[new_df2['Taken']==i,'Aantal'] = int(temp['Aantal'][temp['Taken']==i])
    
    
    if len(new_df[new_df['Aanwezig'] == 1]) > sum(new_df2[new_df2['Aan']==1]['Aantal']):
        aantal_werknemers = len(new_df[new_df['Aanwezig'] == 1])
        aantal_benodigd_voor_taken = sum(new_df2[new_df2['Aan']==1]['Aantal'])
        
        st.write(f'LET OP: Het aantal werknemers is niet gelijk aan het benodigde aantal werknemers voor alle taken. Er zijn {aantal_werknemers} werknemers aanwezig en er zijn {aantal_benodigd_voor_taken} werknemers nodig om alle taken af te kunnen ronden.')
    
    
    st.markdown("#")
    st.markdown("#")
    

    

    
    #Mensen vastzetten op bepaalde taken
    if st.checkbox('Zijn er mensen die per se een bepaalde taak moeten afronden?'):
        aantal_taken = st.number_input("Van hoeveel taken wil je vooraf de mensen opgeven?",min_value=1, value = 1, step = 1)
        speciale_mensen_list = []
        #speciale_mensen_niveau_list = []
        speciale_taken_list = []
        for i in range(int(aantal_taken)):
            speciale_taak = st.selectbox("".join(["Wat is de ",str(i+1),"e taak?"]),new_df2["Taken"][new_df2["Aan"]==1])
            new_df2["Aan"][new_df2["Taken"]==speciale_taak]=0
            speciale_mensen = st.multiselect(
                 "".join(["Wie gaan deze ",str(i+1),"e taak uitvoeren?"]),new_df['Werknemers'][new_df["Aanwezig"]==1])
            if len(speciale_mensen)==int(new_df2["Aantal"][new_df2["Taken"]==speciale_taak]):
                st.write("Je hebt het juiste aantal mensen geselecteerd voor deze taak!")
            else:
                st.write("Je moet nog ",int(new_df2["Aantal"][new_df2["Taken"]==speciale_taak])-len(speciale_mensen)," werknemers kiezen voor deze taak." )
            for i in range(3):
                st.write(" ")
            speciale_mensen_list = speciale_mensen_list + speciale_mensen
            for speciaal_mens in speciale_mensen:
                speciale_taken_list.append(speciale_taak)
                #speciale_mensen_niveau_list.append(new_df[speciale_taak][speciaal_mens])
                mensen_aanwezig_niet_in_planning.append(speciaal_mens)
                new_df["Aanwezig"][new_df["Werknemers"]==speciaal_mens]=0
        df_speciale_taken_mensen = pd.DataFrame([speciale_taken_list,speciale_mensen_list]).T   
        
        #maximum op opgegeven bovengrens capaciteit taak? #!!!!!
        # lege dictionary maken, met de taken als keys
        oplossing1 = defaultdict(list)
        for taak in df_speciale_taken_mensen[0]:
            oplossing1[taak]=[]

        # Werknemers toevoegen aan de values van de keys (taken)
        for i in df_speciale_taken_mensen.index:
            taak = df_speciale_taken_mensen[0][i]
            werknemer = df_speciale_taken_mensen[1][i]
            oplossing1[taak].append(werknemer)
        
        # terug naar een dataframe
        df2 = pd.DataFrame.from_dict(oplossing1, orient = 'index')
        df_pre_concat_list.append(df2)
    
    st.markdown("#")
    # zeelandia
    if st.checkbox('Staat laden/lossen voor Zeelandia vandaag op de planning?',value=True):
        zeelandia = st.multiselect(
            'Wie gaat laden/lossen?',mensen_op_de_werkvloer)
            
        zl = pd.DataFrame.from_dict({'Laden/lossen Zeelandia': zeelandia}, orient = 'index')
        df_pre_concat_list = [zl]+df_pre_concat_list
   
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
        if len(new_df[new_df['Aanwezig'] == 1]) > sum(new_df2[new_df2['Aan']==1]['Aantal']):
            aantal_werknemers = len(new_df[new_df['Aanwezig'] == 1])
            aantal_benodigd_voor_taken = sum(new_df2[new_df2['Aan']==1]['Aantal'])
            st.error(f'LET OP: Het aantal werknemers is niet gelijk aan het benodigde aantal werknemers voor alle taken. Er zijn {aantal_werknemers} werknemers aanwezig en er zijn {aantal_benodigd_voor_taken} werknemers nodig om alle taken af te kunnen ronden.')
        else:
            #uitzendkrachten automatisch aanvullen
            if len(new_df[new_df['Aanwezig'] == 1]) < sum(new_df2[new_df2['Aan']==1]['Aantal']):
                aantal_benodigde_uitzendkrachten = sum(new_df2[new_df2['Aan']==1]['Aantal']) - len(new_df[new_df['Aanwezig'] == 1])
                st.warning(f'WARNING: Het aantal werknemers is kleiner dan het benodigde aantal werknemers voor alle taken. Er zijn {aantal_benodigde_uitzendkrachten} uitzendkrachten toegevoegd om te voldoen aan de hoeveelheid taken.')
                #Voeg uitzendkrachten toe
                for i in range(sum(new_df2[new_df2['Aan']==1]['Aantal']) - len(new_df[new_df['Aanwezig'] == 1])):
                    uitzendkracht_skills = ["".join(["Uitzendkracht ",str(i+1)])] + list(dataframe3.loc[0,dataframe3.columns!="Werknemers"])
                    df_uitzendkracht_skills = pd.DataFrame(uitzendkracht_skills).transpose()
                    df_uitzendkracht_skills.columns = new_df.columns
                    new_df = pd.concat([new_df, df_uitzendkracht_skills], ignore_index = True)
            data_werknemers = new_df
            data_machines = new_df2
            
             
# =============================================================================
#             ''' Data prepareren'''
# =============================================================================

            # check: zijn er genoeg mensen om op de machines te zetten?
            #if len(data_werknemers[data_werknemers['Aanwezig'] == 1]) != sum(data_machines[data_machines['Aan']==1]['Aantal']):
            #    exit('WARNING: Het aantal werknemers is niet gelijk aan het benodigde aantal werknemers voor alle taken.')

            # dataframes aanpassen aan de aanwezige werkenemers
            data_aanwezig = data_werknemers.loc[data_werknemers['Aanwezig'] == 1,:]
            data_taken = data_machines.loc[data_machines['Aan']==1,:]

            # wanneer er bij werknemers geen specifieke taak is gegeven, maken we hier taak -1 van
            #data_aanwezig.loc[data_aanwezig['Taak'].isna(),'Taak'] = -1
            
           
            # de overige ('rest') benodigde competenties verwerken
            for i in data_taken.index:
                # hoeveel mensen er per niveau er al minimaal moeten zijn
                ingepland = data_taken.Aantal_min_niveau_2[i]+data_taken.Aantal_min_niveau_1[i]+data_taken.Aantal_min_niveau_3[i]
                # kijken of er sprake is van een minimaal niveau van de rest/overige
                if data_taken.Aantal[i] > ingepland:
                    # als er iets staat als: 1v1, 2v2 en DE REST NIVEAU 2,dan plannen we hier de 'rest' op niveau 2.
                    # bepaal eerst het restant werknemers, hier genaamd 'overig'
                    niet_ingedeeld = data_taken.Aantal[i] - ingepland
                    data_taken.loc[i,"".join(("Aantal_min_niveau_", str(int(data_taken.Rest_min_niveau[i]))))] += niet_ingedeeld
            
            # dataframe met alleen competenties
            df_comp = data_aanwezig.loc[:,data_taken.Taken]
         
# =============================================================================
#             '''Wiskundig model (1): sets en parameters opstellen'''
# =============================================================================

            # Set van taken, levels en werknemers maken
            levels = range(1,4)
            taken = data_taken.index
            werknemers = data_aanwezig.index

            # parameter aanmaken die zegt of een werknemer een bepaald level op een taak bezit
            vorm_skill = list(product(levels, werknemers, taken))
            skill = np.zeros(len(vorm_skill))

            for taak in taken:
                naam_taak = data_taken.Taken[taak]
                for werknemer in werknemers:
                    for level in levels:
                        
                        if df_comp.loc[werknemer,naam_taak] <= level:
                            ind = vorm_skill.index((level,werknemer,taak))
                            skill[ind] = 1
                           
            # aantal mensen die per level per taak nodig zijn (minimaal niveau)             
            vorm_teamsize = list(product(levels, taken))
            teamsize = np.zeros(len(vorm_teamsize))
            for taak in taken:
                for level in levels:
                    kolomnaam = "".join(("Aantal_min_niveau_", str(level)))
                    aantal = data_taken.loc[taak,kolomnaam]
                    ind = vorm_teamsize.index((level,taak))
                    teamsize[ind] = aantal  
                   
            # parameter die aangeeft of 2 mensen samen kunnen werken
            vorm_taal = list(product(werknemers, werknemers))
            taal = np.ones(len(vorm_taal))
            for werknemer1 in werknemers[:-1]:
                for werknemer2 in werknemers[np.where(werknemers == werknemer1)[0][0]:]:
                    if ((data_aanwezig.Nederlands[werknemer1] + data_aanwezig.Nederlands[werknemer2] == 2) 
                        or (data_aanwezig.Pools[werknemer1] + data_aanwezig.Pools[werknemer2] == 2)):
                        ind = vorm_taal.index((werknemer1,werknemer2))
                        taal[ind] = 0
                        
                        
# =============================================================================
#             '''Wiskundig model (2): model met beslisvariabele, constraints en doelfunctie opstellen'''
# =============================================================================

            # model opstellen
            model = Model(solver_name=CBC) 
            
            # beslisvariabele opstellen
            X = [model.add_var(name="x({},{},{})".format(l,w,t), var_type=BINARY) 
                 for [l, w, t] in product(levels, werknemers, taken)] 
            
            T = [model.add_var(name="t({},{},{})".format(i,j,t), var_type=BINARY) 
                 for [i, j, t] in product(werknemers, werknemers, taken[data_taken.Samenwerken==1])] 
            vorm_T = list(product(werknemers, werknemers, taken[data_taken.Samenwerken==1]))
            
            
            if doelfunctie_keuzebox == 'Iedereen doet waar hij het beste in is':
                # DOELFUNCTIE 1 opstellen (in dit geval zo min mogelijk kosten) #########################################################
                model.objective = maximize(xsum(X[vorm_skill.index((1,w,t))] + 0.5* X[vorm_skill.index((2,w,t))]  for w in werknemers for t in taken))
                
            if doelfunctie_keuzebox == 'Iedereen staat zo veel mogelijk op een machine waar hij nog over moet leren':
                # DOELFUNCTIE 2 opstellen (in dit geval zo veel mogelijk bij de minimum eisen) ##########################################
                U = [model.add_var(name="u({},{})".format(l,t), var_type=INTEGER) 
                     for [l, t] in product(levels, taken)] 
                
                model.objective = minimize(xsum(U[vorm_teamsize.index((l,t))]  for l in levels for t in taken))
                
                for level in levels:
                    for taak in taken:
                        model += xsum(X[vorm_skill.index((level,w,taak))] 
                                      for w in werknemers) - teamsize[vorm_teamsize.index((level,taak))] <= U[vorm_teamsize.index((level,taak))]
                        model += -(xsum(X[vorm_skill.index((level,w,taak))] 
                                      for w in werknemers) - teamsize[vorm_teamsize.index((level,taak))]) <= U[vorm_teamsize.index((level,taak))]
                
            
            if doelfunctie_keuzebox == 'Op de belangrijke taken staan goede mensen, op de rest staan beginners':
                # DOELFUNCTIE 3 combi van 1 en 2
                 
                 U = [model.add_var(name="u({},{})".format(l,t), var_type=INTEGER) 
                      for [l, t] in product(levels, taken)] 
                 
                 model.objective = minimize(xsum(U[vorm_teamsize.index((l,t))]/2  for l in levels for t in taken)
                                            + xsum(X[vorm_skill.index((3,w,t))] for w in werknemers for t in taken)
                                            + xsum(X[vorm_skill.index((2,w,t))]/2 for w in werknemers for t in taken))
                 
                 for level in levels:
                     for taak in taken:
                         model += xsum(X[vorm_skill.index((level,w,taak))] 
                                       for w in werknemers) - teamsize[vorm_teamsize.index((level,taak))] <= U[vorm_teamsize.index((level,taak))]
                         model += -(xsum(X[vorm_skill.index((level,w,taak))] 
                                       for w in werknemers) - teamsize[vorm_teamsize.index((level,taak))]) <= U[vorm_teamsize.index((level,taak))]
                
            
            ### CONSTRAINT 1: iedere persoon krijgt maar 1 taak toegewezen
            for werknemer in werknemers:
                model += (xsum(X[vorm_skill.index((l,werknemer,t))] for l in levels for t in taken) == 1)
                
            ### CONSTRAINT 2: een persoon wordt alleen toegedeeld aan een bepaald level van een taak als hij/zij deze ook bezit
            for level in levels:
                for werknemer in werknemers:
                    for taak in taken:
                        model += (X[vorm_skill.index((level,werknemer,taak))] <= skill[vorm_skill.index((level,werknemer,taak))])
            
            ### CONSTRAINT 3: op iedere taak worden evenveel mensen ingepland als dat er nodig zijn
            for taak in taken:
                aantal_taak = new_df2.loc[taak,'Aantal']
# =============================================================================
#                 for level in levels:
#                     aantal_level = teamsize[vorm_teamsize.index((level,taak))]
#                     aantal_taak += aantal_level
# =============================================================================
                model += (xsum(X[vorm_skill.index((l,w,taak))] for l in levels for w in werknemers) == aantal_taak)
                
            ### CONSTRAINT 4: taak wordt alleen uitgevoerd op minimum level
            for taak in taken:
                aantal_min_level1 = data_taken.loc[taak,'Aantal_min_niveau_1']
                model += (xsum(X[vorm_skill.index((1,w,taak))] for w in werknemers) >= aantal_min_level1)
                aantal_max_level3 = data_taken.loc[taak,'Aantal_min_niveau_3']
                model += (xsum(X[vorm_skill.index((3,w,taak))] for w in werknemers) <= aantal_max_level3)
                        
            ### CONSTRAINT 5: mensen spreken dezelfde taal waar nodig
            # eerst zorgen dat variabele T goed is gefenieerd; dus 1 als mensen samenwerken
            for i in werknemers:
                ind = werknemers.tolist().index(i)
                for j in werknemers[ind+1:]:
                    for taak in taken[data_taken.Samenwerken==1]:
                        # x >= z;  y>= z;  x+y-1 <= z   ----> T = 1 als mensen samenwerken (x en y beide 1 zijn)
                        model += (xsum(X[vorm_skill.index((l,i,taak))] for l in levels ) >= T[vorm_T.index((i,j,taak))])
                        model += (xsum(X[vorm_skill.index((l,j,taak))] for l in levels ) >= T[vorm_T.index((i,j,taak))])
                        model += (xsum(X[vorm_skill.index((l,i,taak))] + X[vorm_skill.index((l,j,taak))] 
                                       for l in levels ) -1 <= T[vorm_T.index((i,j,taak))])
            
            for taak in taken[data_taken.Samenwerken==1]:
                model += (xsum(T[vorm_T.index((i,j,taak))] * taal[vorm_taal.index((i,j))] 
                                   for i in werknemers for j in werknemers) == 0)
                            

# =============================================================================
#             '''Wiskundig model (3): Oplossing eruit halen'''
# =============================================================================
            
#            """Indexen van constraints definieren"""
            W = len(werknemers)
            T = len(taken)
            L = len(levels)
            
            ## ALS DOELFUNCTIE 1 WORDT GEBRUIKT
            if doelfunctie_keuzebox == 'Iedereen doet waar hij het beste in is':
                aantal_c = [W, L*W*T, T, T*2, ((W-1)*W/2)*sum(data_taken.Samenwerken)*3 + sum(data_taken.Samenwerken)]
            
            ## ALS DOELFUNCTIE 2 OF 3 WORDT GEBRUIKT
            else:
                aantal_c = [L*T*2, W, L*W*T, T, T*2, ((W-1)*W/2)*sum(data_taken.Samenwerken)*3 + sum(data_taken.Samenwerken)]
            
            cum_aantal_c = np.cumsum(aantal_c).astype(int)
            indexen = [[0,cum_aantal_c[0]]]
            for i in range(len(cum_aantal_c)-1):
                indexen.append([cum_aantal_c[i],cum_aantal_c[i+1]])
            
            # lege oplossing
            solution = pd.DataFrame(columns = ['x','level','werknemer','taak','waarde'])
            
            count = 0
            status_relax = OptimizationStatus.INFEASIBLE
            status = model.optimize(max_seconds=300)
            if status == OptimizationStatus.INFEASIBLE:
                poging = 1
                while status_relax == OptimizationStatus.INFEASIBLE:
                    model_relax = model.copy()
                    if poging == 1:
                        model_relax.remove(model_relax.constrs[indexen[4][0]:indexen[4][1]])
                        status_relax = model_relax.optimize(max_seconds=300)                
                    elif poging == 2:
                        model_relax.remove(model_relax.constrs[indexen[3][0] + int((indexen[3][1]-indexen[3][0]+1)/2) :indexen[3][1] ])
                        status_relax = model_relax.optimize(max_seconds=300)
                    elif poging == 3:
                        model_relax.remove(model_relax.constrs[indexen[3][0]:indexen[3][1]])
                        status_relax = model_relax.optimize(max_seconds=300)
                    elif poging == 4:
                        model_relax.remove(model_relax.constrs[indexen[4][0]:indexen[4][1]])
                        model_relax.remove(model_relax.constrs[indexen[3][0]:indexen[3][1]])
                        status_relax = model_relax.optimize(max_seconds=300)
                    elif poging > 4:
                        status_relax = 1
                        
                        mensen_alleen_4 = []
                        for werknemer in werknemers:           
                            count = 0
                            for level in levels:
                                for taak in taken:
                                    count += skill[vorm_skill.index((level,werknemer,taak))]
                            if count == 0:
                                mensen_alleen_4.append(data_aanwezig.Werknemers[werknemer])

                        if len(mensen_alleen_4) == 1:
                            st.warning(mensen_alleen_4[0] + ' bezit op de huidige taken alleen niveau 4, diegene kan dus nergens ingepland worden en daardoor loopt de planning fout. Selecteer een andere taak, een andere medewerker of pas het competentieniveau van deze persoon aan.')
                        if len(mensen_alleen_4) > 1: 
                            st.warning( ' en '.join(mensen_alleen_4) + ''' bezitten alleen niveau 4 op de huidige taken. Zij kunnen dus niet worden ingepland en daardoor loopt de planning fout. 
                                       Selecteer andere taken, anderen medewerkers of pas de competentieniveaus van deze personen aan.\n''')
                        
                        taken_alleen_4 = []
                        for taak in taken:
                            count = 0
                            for level in levels:
                                for werknemer in werknemers:
                                    count += skill[vorm_skill.index((level,werknemer,taak))]
                            if count == 0:
                                taken_alleen_4.append(data_taken.Taken[taak])
                                    
                        if len(taken_alleen_4) == 1:
                            st.warning('Voor de taak ' + taken_alleen_4[0] + ' is niemand aanwezig die niet niveau 4 heeft. Daardoor kan er niemand worden ingepland. Zorg ervoor dat er een werknemer met een ander niveau aanwezig is, of dat de taak niet wordt uitgevoerd.')
                        if len(taken_alleen_4) > 1: 
                            st.warning('Voor de taken ' + ' en '.join(taken_alleen_4) + ''' zijn er geen werknemers aanwezig die geen niveau 4 hebben. Daardoor kan er niemand worden ingepland. 
                                       Zorg ervoor dat er werknemers met een ander niveau aanwezig zijn, of dat de taken niet worden uitgevoerd.\n''')
                        
                        
                        st.write('Er kan helaas geen oplossing gevonden worden.')
                    poging += 1
                if status_relax == OptimizationStatus.OPTIMAL:
                    count = 0
                    for v in model_relax.vars:
                        if v.name[0] == 'x':
                            solution.loc[count,'x'] = v.name
                            [v1,v2,v3] = v.name.split('(')[1].split(')')[0].split(',')
                            solution.loc[count,'level'] = v1
                            solution.loc[count,'werknemer'] = v2
                            solution.loc[count,'taak'] = v3
                            solution.loc[count,'waarde'] = v.x
                        count += 1
                    if (poging == 2):
                        st.warning('''LET OP! De planning voldoet niet aan de volgende eis:\n
* Werknemers spreken niet overal dezelfde taal, waar nodig''')
                    if (poging == 3):
                        st.warning('''LET OP! De planning voldoet niet aan de volgende eis:\n
* Op de taken zijn er voldoende werknemers op niveau 1 ingedeeld, maar er wordt niet voldaan aan de minimum eisen van niveau 2 en 3''')
                    if (poging == 4):
                        st.warning('''LET OP! De planning voldoet niet aan de volgende eis:\n
* Er wordt niet aan de minimum eisen van de niveaus van de taken voldaan''')
                    if poging == 5:
                        st.warning('''LET OP! De planning voldoet niet aan de volgende eisen: \n 
* Er wordt niet aan de minimum eisen van de niveaus van de taken voldaan \n
* Werknemers spreken niet overal dezelfde taal, waar nodig''')
            
            elif status == OptimizationStatus.OPTIMAL:
                count = 0
                for v in model.vars:
                    if v.name[0] == 'x':
                        solution.loc[count,'x'] = v.name
                        [v1,v2,v3] = v.name.split('(')[1].split(')')[0].split(',')
                        solution.loc[count,'level'] = v1
                        solution.loc[count,'werknemer'] = v2
                        solution.loc[count,'taak'] = v3
                        solution.loc[count,'waarde'] = v.x
                    count += 1

            else:
                st.write('Model status is niet optimaal, maar ook niet infeasible')

            # dataframe aanpassen naar wat we terug willen geven
            
            
            
            solution_def = solution.loc[solution['waarde']==1,['werknemer','taak','level']]
            for mens in solution_def.index:
                naam_ind = int(solution_def.werknemer[mens])
                naam = data_aanwezig.Werknemers[naam_ind]
                solution_def.loc[mens,'werknemer'] = naam
                
                taak_ind = int(solution_def.taak[mens])
                taak = data_taken.Taken[taak_ind]
                solution_def.loc[mens,'taak'] = taak

# =============================================================================
# HIER EINDIGD HET WISKUNDIG MODEL    
# =============================================================================



# =============================================================================
#             ''' Oplossing in dictionary + terug naar dataframe'''
# =============================================================================


            # lege dictionary maken, met de taken als keys
            oplossing = defaultdict(list)
            for taak in data_taken.Taken:
                oplossing[taak]=[]

            # Werknemers toevoegen aan de values van de keys (taken)
            for i in solution_def.index:
                taak = solution_def.taak[i]
                werknemer = solution_def.werknemer[i]
                oplossing[taak].append(werknemer)
                
            

            # terug naar een dataframe
            df = pd.DataFrame.from_dict(oplossing, orient = 'index')
            df_pre_concat_list.append(df)
            df = pd.concat(df_pre_concat_list)
            
            
            
            ######## df splitsen in linker en rechter deel
            # lijst van alle taken + lijst van waar de taken horen
            alle_taken = ['Laden/lossen Zeelandia'] + list(data_machines['Taken'])
            verdeling = [1] + list(data_machines['Verdeling oud planbord'])
            taken_rechts = data_machines['Verdeling oud planbord'][data_machines['Verdeling oud planbord']==2].index
            
            # lege dataframes
            df_links = pd.DataFrame()
            df_rechts = pd.DataFrame()
            
            # voor iedere taak in df, kijken of die links of rechts moet; en hier toevoegen.
# =============================================================================
#             for taak in alle_taken:
#                 if taak in df.index:
#                     ind = list(alle_taken).index(taak)
#                     kant = verdeling[ind]
#                     if kant == 1:
#                         df_links = pd.concat([df_links,df.loc[taak,:]],axis = 1)
#                     elif kant == 2:
#                         df_rechts = pd.concat([df_rechts,df.loc[taak,:]],axis = 1)
#                 elif alle_taken.index(taak)-1 in taken_rechts:
#                     dumm = pd.DataFrame(columns=[taak],index=df.columns)
#                     df_rechts = pd.concat([df_rechts,dumm],axis = 1)
# =============================================================================
                    
            for taak in df.index:
            #if taak in data_machines['Taken']:
                ind = list(alle_taken).index(taak)
                kant = verdeling[ind]
                if kant == 1:
                    df_links = pd.concat([df_links,df.loc[taak,:]],axis = 1)
                elif kant == 2:
                    df_rechts = pd.concat([df_rechts,df.loc[taak,:]],axis = 1)
            
            
                
                
                
            df_links = df_links.T
            df_rechts = df_rechts.T
            
            df_links = pd.concat([df_links,df_eenmalig])
            
            df_links.fillna('', inplace=True)
            df_rechts.fillna('', inplace=True)
            df.fillna('', inplace=True)
            
            
            # array van werknemers die afwezig zijn
            afwezig = data_werknemers[data_werknemers['Aanwezig'] == 0].Werknemers
            afwezig = set(afwezig)-set(mensen_aanwezig_niet_in_planning)
            afwezig = pd.DataFrame(afwezig)
            
            # header in df toevoegen
# =============================================================================
#             if (status==OptimizationStatus.OPTIMAL) or (status_relax==OptimizationStatus.OPTIMAL):
#                 colnames = []
#                 for i in range(len(df.columns)):
#                     colnames.append("".join(["Werknemer ",str(i+1)]))
#                 df.columns = colnames
#                 st.dataframe(df)
#                 
# =============================================================================
            
            if (status==OptimizationStatus.OPTIMAL) or (status_relax==OptimizationStatus.OPTIMAL):
                colnames = []
                for i in range(len(df_links.columns)):
                    colnames.append("".join(["Werknemer ",str(i+1)]))
                df_links.columns = colnames
                colnames = []
                for i in range(len(df_rechts.columns)):
                    colnames.append("".join(["Werknemer ",str(i+1)]))
                df_rechts.columns = colnames
                
                dftijdelijk = pd.concat([df_links,df_rechts])
                dftijdelijk.fillna('', inplace=True)
                st.dataframe(dftijdelijk)
  
  

            #De planning krijgt hier een gepaste naam, waarna de planning te downloaden is via een button
            bestandsnaam = 'Dagplanning.xlsx'
            
# =============================================================================
# planning naar excel
# =============================================================================
            #import pandas.io.formats.excel

            #pd.io.formats.excel.ExcelFormatter.header_style = None
#-------------------------------------------------------------
        
            def to_excel(df):
                output = BytesIO()
                writer = pd.ExcelWriter(output, engine='xlsxwriter')
                
                def num_to_let(i):
                    return chr(ord('a')+i).capitalize()
                    
                
                # definieer start en eindpunten
                startrow_l = 2 + 5
                endrow_l = df_links.shape[0] + 2 + 5
                startrow_r = df_links.shape[0] + 3 + 5
                endrow_r = df_links.shape[0] + 3 + df_rechts.shape[0] + 5
                endcol = max(df_links.shape[1],df_rechts.shape[1])
                endcol_letter = num_to_let(endcol)
                
                # dataframes naar excel omzetten
                df_links.to_excel(writer, index=True, sheet_name='Planning',startrow = startrow_l-2)
                df_rechts.to_excel(writer, index=True, sheet_name='Planning',startrow = startrow_r-1,header=False)
                afwezig.to_excel(writer, sheet_name='Planning', index = False, startrow = startrow_l-2, startcol=len(df.columns)+3)
                
                # define workbook and worksheet
                workbook = writer.book
                worksheet = writer.sheets['Planning']
                
            
                
                # define format tabel
                line_gr = workbook.add_format({'bg_color': '#d2e1e9'})
                line_w = workbook.add_format({'bg_color': 'white'})
                line_gr_border = workbook.add_format({'bg_color': '#d2e1e9','left': 1,'right':1})
                line_w_border = workbook.add_format({'bg_color': 'white','left': 1,'right':1})
                #w_top_border = workbook.add_format({'bg_color': 'white','top': 1,'left':1,'right':1})

                def color_line_by_line(first_row, last_row, first_col, last_col, f1, f2):
                    for l in range(first_row, last_row):
                        line = "".join([first_col,str(l),':',last_col,str(l)])
                        if (l-first_row)/2 == int((l-first_row)/2):
                            worksheet.conditional_format(line, {'type':'cell',
                                                'criteria': 'not equal to','value': 50,'format': f1})
                        else:
                            worksheet.conditional_format(line, {'type':'cell',
                                                'criteria': 'not equal to','value': 50,'format': f2})
                
                
                
                color_line_by_line(startrow_l,endrow_l,'B',endcol_letter,line_w,line_gr)
                color_line_by_line(startrow_r,endrow_r,'B',endcol_letter,line_w,line_gr)
                color_line_by_line(startrow_l,endrow_l,'A','A',line_w_border,line_gr_border)
                color_line_by_line(startrow_r,endrow_r,'A','A',line_w_border,line_gr_border)
                color_line_by_line(startrow_l,endrow_l,num_to_let(endcol+1),num_to_let(endcol+2),line_w_border,line_gr_border)
                color_line_by_line(startrow_r,endrow_r,num_to_let(endcol+1),num_to_let(endcol+2),line_w_border,line_gr_border)



                # define format
                novo_format = workbook.add_format({'bold': 1,'border': 1,'align': 'center',
                    'valign': 'vcenter','bg_color': '#59B6AD'})
                afwezigen_format = workbook.add_format({'bold': 1,'border': 1,
                    'align': 'center','valign': 'vcenter','bg_color': '#FF6103'})

                # format afwezigheidskolom
                oranje = workbook.add_format({'right':1,'bg_color': '#ffead3'})
                k = num_to_let(endcol+3)
                bereik = "".join([k,str(startrow_l),':',k,str(len(afwezig)+startrow_l-1)])
                worksheet.conditional_format(bereik, {'type':'cell',
                                    'criteria': 'not equal to','value': 50,'format': oranje})


                # format header aanpassen
                range_header = "".join(['A',str(startrow_l-1),':',num_to_let(endcol+2),str(startrow_l-1)])
                #range_header = "".join(['A',str(endrow_l),':',num_to_let(endcol+2),str(endrow_l)])
                worksheet.conditional_format(range_header, {'type':'cell',
                                    'criteria': 'not equal to','value': 50,'format': novo_format})
                worksheet.write(startrow_l-2,0, 'Taken',novo_format)
                worksheet.merge_range("".join(['B',str(startrow_l-1),':',endcol_letter,str(startrow_l-1)]), 'Werknemers', novo_format)
                worksheet.merge_range("".join([num_to_let(endcol+1),str(startrow_l-1),':',num_to_let(endcol+2),str(startrow_l-1)]), 'Bijzonderheden', novo_format)
                
                range_header = "".join([num_to_let(endcol+3),str(startrow_l-1),':',num_to_let(endcol+3),str(startrow_l-1)])
                worksheet.conditional_format(range_header, {'type':'cell',
                                    'criteria': 'not equal to','value': 50,'format': afwezigen_format})
                worksheet.write(startrow_l-2,len(df.columns)+3, 'Afwezigen', afwezigen_format)
                
                
                # format scheidingsregel 
                range_line = "".join(['A',str(endrow_l),':',num_to_let(endcol+2),str(endrow_l)])
                worksheet.conditional_format(range_line, {'type':'cell',
                                    'criteria': 'not equal to','value': 50,'format': novo_format})
                worksheet.merge_range("".join(['B',str(endrow_l),':',endcol_letter,str(endrow_l)]),'')
                worksheet.merge_range("".join([num_to_let(endcol+1),str(endrow_l),':',num_to_let(endcol+2),str(endrow_l)]),'')
                #range_header = "".join(['B',row,':',chr(ord('a')+len(df.columns)).capitalize(),row])
                #worksheet.merge_range(range_header, '', novo_format)

                # format buitenste lijnen
                format_top_border = workbook.add_format({'top':1})
                format_top_und_border = workbook.add_format({'top':1,'bottom':1})
                bereik = "".join(['A',str(endrow_r),':',num_to_let(endcol+2),str(endrow_r)])
                worksheet.conditional_format(bereik, {'type':'cell',
                                    'criteria': 'not equal to','value': 50,'format': format_top_und_border})
                row_under_last_a = str(len(afwezig)+startrow_l)
                worksheet.conditional_format("".join([num_to_let(endcol+3),row_under_last_a]), {'type':'cell',
                                    'criteria': 'not equal to','value': 50,'format': format_top_border})
                
                
                # novo logo
                url = 'https://github.com/NovoPW/Planningstool/blob/main/NOVO-Logo.png?raw=true'
                image_data = BytesIO(urlopen(url).read())
                worksheet.insert_image(0,0, url,{'image_data': image_data,'x_scale': 0.3, 'y_scale': 0.3})
                
                bold_format = workbook.add_format({'bold': 1,'align':'center'})
                # dag van de week
                # frikandellendaggg
                if dag=='vrijdag':
                    url2 = 'https://github.com/NovoPW/Planningstool/blob/main/download.png?raw=true'
                    image_data = BytesIO(urlopen(url2).read())
                    worksheet.insert_image(0,3, url2,{'image_data': image_data,'x_scale': 0.5, 'y_scale': 0.5})
                    worksheet.write(4,3,"Frikandellen-Vrijdag",bold_format)
                
                else:
                    big_bold_format = workbook.add_format({'bold': 1,'font_size':25})
                    worksheet.merge_range('D3:E4',dag.capitalize(),big_bold_format)
                


                bold_format = workbook.add_format({'bold': 1})
                # opmerkingen box maken
                worksheet.write(endrow_r,0,'Opmerkingen:',bold_format)
                
                
                
                #format_box = workbook.add_format({'border': 1})
                #worksheet.conditional_format(0, endrow_r, endcol+2, endrow_r+3, {'type': 'cell','format': format_box})
                
                left_border = workbook.add_format({'left':1,'align':'left','bold':0})
                right_border = workbook.add_format({'right':1})
                worksheet.conditional_format("".join(['A',str(endrow_r-1),':A',str(endrow_r+5)]), {'type':'cell',
                                    'criteria': 'not equal to','value': 50,'format': left_border})
                worksheet.conditional_format("".join([num_to_let(endcol+2),str(endrow_r-1),':',num_to_let(endcol+2),str(endrow_r+5)]), {'type':'cell',
                                    'criteria': 'not equal to','value': 50,'format': right_border})
               # worksheet.conditional_format("".join(['A',str(endrow_r+1),':',num_to_let(endcol+4),str(endrow_r+1)]), {'type':'cell',
                #                    'criteria': 'not equal to','value': 50,'format': format_top_border})
                worksheet.conditional_format("".join(['A',str(endrow_r+6),':',num_to_let(endcol+2),str(endrow_r+6)]), {'type':'cell',
                                    'criteria': 'not equal to','value': 50,'format': format_top_border})

                bold_format = workbook.add_format({'bold': 1,'align':'center'})
                # adjust werkenemrs column
                for column in range(len(df.columns)):
                    maximum_colums = df[df.columns[column]].str.len().max()
                    writer.sheets['Planning'].set_column(column + 1, column + 1, maximum_colums + 1)
            
                # adjust index column
                maximum_index = df.index.str.len().max()
                writer.sheets['Planning'].set_column(0, 0, maximum_index + 1)
                worksheet.conditional_format("".join(['A1:A',str(endrow_r+1)]), {'type':'cell',
                                    'criteria': 'not equal to','value': 50,'format': bold_format})
                
            
                # adjust afwezigheid column
                if afwezig.empty:
                    maximum_index = 10  # Fallback-waarde als de DataFrame leeg is
                else:
                    maximum_index = afwezig[0].str.len().max()
                
                writer.sheets['Planning'].set_column(len(df.columns)+3, len(df.columns)+3, maximum_index + 1)
                
                # adjust comments column
                writer.sheets['Planning'].set_column(len(df.columns)+1, len(df.columns)+2,15)
                
                
                worksheet.conditional_format('A1:Z51', {'type':'cell',
                                    'criteria': 'not equal to','value': 50,'format': line_w})
                writer.save()
                processed_data = output.getvalue()
            
                return processed_data
            df_xlsx = to_excel(df)
            st.download_button(label='📥 Download planning als Excel bestand',
                                            data=df_xlsx ,
                                            file_name= bestandsnaam)
          
#leegte om de disclaimer onderaan de pagina te krijgen
for i in range(20):
    st.text("")

#De disclaimer
st.write("""
         ###### Disclaimer
         Deze tool is gemaakt door Rachelle Hermans en Emile Baljeu. Zij zijn, evenals Fontys Hogescholen, niet aansprakelijk voor mogelijke complicaties tijdens en/of na het gebruik van deze site. Ook hebben zij geen rechten voor het gebruiken van het logo op deze site, dus klaag ze alstublieft niet aan. Groetjes!""")
# =============================================================================
# if st.checkbox('Leuke foto van de designers zien?'):         
#          st.image("https://raw.githubusercontent.com/NovoPW/Planningstool/main/Stage_is_leuk.png")
#          
# =============================================================================
         
#OM TE RUNNEN:

#open terminal van ML via anaconda navigator
#(eventueel conda avtivate ML)
#typ: cd C:\Users\Émile\Desktop\School\TW3\Stage\python
#typ: streamlit run streamlit_stage.py
#of typ
#streamlit run C:\Users\Émile\Desktop\School\TW3\Stage\Planningstool_code.py

#hulplinkjes
#deploy de app https://www.youtube.com/watch?v=kXvmqg8hc70
#uitbreiden https://www.youtube.com/watch?v=JwSS70SZdyM
#streamlit documentatie https://docs.streamlit.io/library/get-started
#editable grid?????? https://github.com/PablocFonseca/streamlit-aggrid
