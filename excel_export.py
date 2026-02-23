"""Excel export with xlsxwriter formatting and NOVO branding."""

from io import BytesIO
from urllib.request import urlopen

import pandas as pd

DEFAULT_LOGO_URL = 'https://github.com/NovoPW/Planningstool/blob/main/NOVO-Logo.png?raw=true'
DEFAULT_FRIDAY_IMAGE_URL = 'https://github.com/NovoPW/Planningstool/blob/main/download.png?raw=true'


def num_to_letter(i):
    """Convert 0-based column index to Excel column letter."""
    return chr(ord('a') + i).capitalize()


def color_line_by_line(worksheet, first_row, last_row, first_col, last_col, f1, f2):
    """Apply alternating row colors between first_row and last_row."""
    for row in range(first_row, last_row):
        line = f"{first_col}{row}:{last_col}{row}"
        if (row - first_row) / 2 == int((row - first_row) / 2):
            worksheet.conditional_format(line, {
                'type': 'cell', 'criteria': 'not equal to', 'value': 50, 'format': f1
            })
        else:
            worksheet.conditional_format(line, {
                'type': 'cell', 'criteria': 'not equal to', 'value': 50, 'format': f2
            })


def generate_excel(left_board_df, right_board_df, full_planning_df, afwezig, dag,
                   logo_url=DEFAULT_LOGO_URL, friday_image_url=DEFAULT_FRIDAY_IMAGE_URL):
    """Generate a formatted Excel file and return its bytes.

    Parameters
    ----------
    left_board_df : pd.DataFrame - left board tasks with renamed columns
    right_board_df : pd.DataFrame - right board tasks with renamed columns
    full_planning_df : pd.DataFrame - combined planning used for column sizing
    afwezig : pd.DataFrame - absent workers
    dag : str - day name (Dutch)
    logo_url : str or None - URL for NOVO logo; None skips the image
    friday_image_url : str or None - URL for Friday image; None skips the image
    """
    df = full_planning_df
    output = BytesIO()
    writer = pd.ExcelWriter(output, engine='xlsxwriter')

    # definieer start en eindpunten
    startrow_l = 2 + 5
    endrow_l = left_board_df.shape[0] + 2 + 5
    startrow_r = left_board_df.shape[0] + 3 + 5
    endrow_r = left_board_df.shape[0] + 3 + right_board_df.shape[0] + 5
    endcol = max(left_board_df.shape[1], right_board_df.shape[1])
    endcol_letter = num_to_letter(endcol)

    # dataframes naar excel omzetten
    left_board_df.to_excel(writer, index=True, sheet_name='Planning', startrow=startrow_l - 2)
    right_board_df.to_excel(writer, index=True, sheet_name='Planning', startrow=startrow_r - 1, header=False)
    afwezig.to_excel(writer, sheet_name='Planning', index=False, startrow=startrow_l - 2, startcol=len(df.columns) + 3)

    # define workbook and worksheet
    workbook = writer.book
    worksheet = writer.sheets['Planning']

    # define format tabel
    line_gr = workbook.add_format({'bg_color': '#d2e1e9'})
    line_w = workbook.add_format({'bg_color': 'white'})
    line_gr_border = workbook.add_format({'bg_color': '#d2e1e9', 'left': 1, 'right': 1})
    line_w_border = workbook.add_format({'bg_color': 'white', 'left': 1, 'right': 1})

    color_line_by_line(worksheet, startrow_l, endrow_l, 'B', endcol_letter, line_w, line_gr)
    color_line_by_line(worksheet, startrow_r, endrow_r, 'B', endcol_letter, line_w, line_gr)
    color_line_by_line(worksheet, startrow_l, endrow_l, 'A', 'A', line_w_border, line_gr_border)
    color_line_by_line(worksheet, startrow_r, endrow_r, 'A', 'A', line_w_border, line_gr_border)
    color_line_by_line(worksheet, startrow_l, endrow_l, num_to_letter(endcol + 1), num_to_letter(endcol + 2), line_w_border, line_gr_border)
    color_line_by_line(worksheet, startrow_r, endrow_r, num_to_letter(endcol + 1), num_to_letter(endcol + 2), line_w_border, line_gr_border)

    # define format
    novo_format = workbook.add_format({
        'bold': 1, 'border': 1, 'align': 'center', 'valign': 'vcenter', 'bg_color': '#59B6AD'
    })
    afwezigen_format = workbook.add_format({
        'bold': 1, 'border': 1, 'align': 'center', 'valign': 'vcenter', 'bg_color': '#FF6103'
    })

    # format afwezigheidskolom
    oranje = workbook.add_format({'right': 1, 'bg_color': '#ffead3'})
    absent_col_letter = num_to_letter(endcol + 3)
    bereik = f"{absent_col_letter}{startrow_l}:{absent_col_letter}{len(afwezig)+startrow_l-1}"
    worksheet.conditional_format(bereik, {
        'type': 'cell', 'criteria': 'not equal to', 'value': 50, 'format': oranje
    })

    # format header aanpassen
    range_header = f"A{startrow_l-1}:{num_to_letter(endcol+2)}{startrow_l-1}"
    worksheet.conditional_format(range_header, {
        'type': 'cell', 'criteria': 'not equal to', 'value': 50, 'format': novo_format
    })
    worksheet.write(startrow_l - 2, 0, 'Taken', novo_format)
    worksheet.merge_range(f"B{startrow_l-1}:{endcol_letter}{startrow_l-1}", 'Werknemers', novo_format)
    worksheet.merge_range(f"{num_to_letter(endcol+1)}{startrow_l-1}:{num_to_letter(endcol+2)}{startrow_l-1}", 'Bijzonderheden', novo_format)

    range_header = f"{num_to_letter(endcol+3)}{startrow_l-1}:{num_to_letter(endcol+3)}{startrow_l-1}"
    worksheet.conditional_format(range_header, {
        'type': 'cell', 'criteria': 'not equal to', 'value': 50, 'format': afwezigen_format
    })
    worksheet.write(startrow_l - 2, len(df.columns) + 3, 'Afwezigen', afwezigen_format)

    # format scheidingsregel
    range_line = f"A{endrow_l}:{num_to_letter(endcol+2)}{endrow_l}"
    worksheet.conditional_format(range_line, {
        'type': 'cell', 'criteria': 'not equal to', 'value': 50, 'format': novo_format
    })
    worksheet.merge_range(f"B{endrow_l}:{endcol_letter}{endrow_l}", '')
    worksheet.merge_range(f"{num_to_letter(endcol+1)}{endrow_l}:{num_to_letter(endcol+2)}{endrow_l}", '')

    # format buitenste lijnen
    format_top_border = workbook.add_format({'top': 1})
    format_top_und_border = workbook.add_format({'top': 1, 'bottom': 1})
    bereik = f"A{endrow_r}:{num_to_letter(endcol+2)}{endrow_r}"
    worksheet.conditional_format(bereik, {
        'type': 'cell', 'criteria': 'not equal to', 'value': 50, 'format': format_top_und_border
    })
    row_under_last_a = str(len(afwezig) + startrow_l)
    worksheet.conditional_format(f"{num_to_letter(endcol+3)}{row_under_last_a}", {
        'type': 'cell', 'criteria': 'not equal to', 'value': 50, 'format': format_top_border
    })

    # novo logo
    if logo_url is not None:
        image_data = BytesIO(urlopen(logo_url).read())
        worksheet.insert_image(0, 0, logo_url, {'image_data': image_data, 'x_scale': 0.3, 'y_scale': 0.3})

    bold_format = workbook.add_format({'bold': 1, 'align': 'center'})
    if dag == 'vrijdag' and friday_image_url is not None:
        image_data = BytesIO(urlopen(friday_image_url).read())
        worksheet.insert_image(0, 3, friday_image_url, {'image_data': image_data, 'x_scale': 0.5, 'y_scale': 0.5})
        worksheet.write(4, 3, "Frikandellen-Vrijdag", bold_format)
    else:
        big_bold_format = workbook.add_format({'bold': 1, 'font_size': 25})
        worksheet.merge_range('D3:E4', dag.capitalize(), big_bold_format)

    bold_format = workbook.add_format({'bold': 1})
    # opmerkingen box maken
    worksheet.write(endrow_r, 0, 'Opmerkingen:', bold_format)

    left_border = workbook.add_format({'left': 1, 'align': 'left', 'bold': 0})
    right_border = workbook.add_format({'right': 1})
    worksheet.conditional_format(f"A{endrow_r-1}:A{endrow_r+5}", {
        'type': 'cell', 'criteria': 'not equal to', 'value': 50, 'format': left_border
    })
    worksheet.conditional_format(f"{num_to_letter(endcol+2)}{endrow_r-1}:{num_to_letter(endcol+2)}{endrow_r+5}", {
        'type': 'cell', 'criteria': 'not equal to', 'value': 50, 'format': right_border
    })
    worksheet.conditional_format(f"A{endrow_r+6}:{num_to_letter(endcol+2)}{endrow_r+6}", {
        'type': 'cell', 'criteria': 'not equal to', 'value': 50, 'format': format_top_border
    })

    bold_format = workbook.add_format({'bold': 1, 'align': 'center'})
    # adjust werknemers column
    for column in range(len(df.columns)):
        maximum_colums = df[df.columns[column]].str.len().max()
        writer.sheets['Planning'].set_column(column + 1, column + 1, maximum_colums + 1)

    # adjust index column
    maximum_index = df.index.str.len().max()
    writer.sheets['Planning'].set_column(0, 0, maximum_index + 1)
    worksheet.conditional_format(f"A1:A{endrow_r+1}", {
        'type': 'cell', 'criteria': 'not equal to', 'value': 50, 'format': bold_format
    })

    # adjust afwezigheid column
    if afwezig.empty:
        maximum_index = 10
    else:
        maximum_index = afwezig[0].str.len().max()

    writer.sheets['Planning'].set_column(len(df.columns) + 3, len(df.columns) + 3, maximum_index + 1)

    # adjust comments column
    writer.sheets['Planning'].set_column(len(df.columns) + 1, len(df.columns) + 2, 15)

    worksheet.conditional_format('A1:Z51', {
        'type': 'cell', 'criteria': 'not equal to', 'value': 50, 'format': line_w
    })
    writer.close()
    return output.getvalue()
