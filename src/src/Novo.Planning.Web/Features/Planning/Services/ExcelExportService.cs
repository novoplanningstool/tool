using ClosedXML.Excel;
using Novo.Planning.Domain.Interfaces;
using Novo.Planning.Domain.Models;

namespace Novo.Planning.Web.Features.Planning.Services;

public class ExcelExportService : IExcelExportService
{
    private static readonly XLColor TealColor = XLColor.FromHtml("#59B6AD");
    private static readonly XLColor OrangeColor = XLColor.FromHtml("#FF6103");
    private static readonly XLColor GrayColor = XLColor.FromHtml("#d2e1e9");
    private static readonly XLColor LightOrangeColor = XLColor.FromHtml("#FCD5B4");

    private const int ColTaken = 1;        // A
    private const int ColWorkersStart = 2; // B
    private const int ColWorkersEnd = 6;   // F
    private const int ColBijzG = 7;        // G
    private const int ColBijzH = 8;        // H
    private const int ColAfwezig = 9;      // I
    private const int ColZ = 26;

    private readonly ITaskDefinitionRepository _taskRepository;
    private readonly IWebHostEnvironment _environment;

    public ExcelExportService(ITaskDefinitionRepository taskRepository, IWebHostEnvironment environment)
    {
        _taskRepository = taskRepository;
        _environment = environment;
    }

    public byte[] Export(PlanningModel planning)
    {
        var tasks = _taskRepository.GetAllAsync().GetAwaiter().GetResult();

        var assignmentsByTask = planning.Assignments
            .Where(a => !string.IsNullOrEmpty(a.TaskName))
            .GroupBy(a => a.TaskName)
            .ToDictionary(g => g.Key, g => g.Select(a => a.WorkerName).ToList());

        var customTaskDefs = planning.CustomTasks
            .Where(ct => assignmentsByTask.ContainsKey(ct.TaskName))
            .Select(ct => ct.ToTaskDefinition())
            .ToList();

        var orderedTasks = tasks
            .Where(t => assignmentsByTask.ContainsKey(t.Name))
            .Concat(customTaskDefs)
            .OrderBy(t => t.SortOrder)
            .ToList();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add(planning.DayName);

        // ============================================================
        // 0. White borderless canvas — rows 1-50, columns A-Z
        // ============================================================
        var canvas = ws.Range(1, 1, 50, ColZ);
        canvas.Style.Fill.BackgroundColor = XLColor.White;
        canvas.Style.Border.TopBorder = XLBorderStyleValues.None;
        canvas.Style.Border.BottomBorder = XLBorderStyleValues.None;
        canvas.Style.Border.LeftBorder = XLBorderStyleValues.None;
        canvas.Style.Border.RightBorder = XLBorderStyleValues.None;
        ws.ShowGridLines = false;

        // ============================================================
        // 1. Logo — floating, 5 rows high, not attached to a cell
        // ============================================================
        var logoPath = Path.Combine(_environment.WebRootPath, "NOVO-Logo.png");
        if (File.Exists(logoPath))
        {
            var picture = ws.AddPicture(logoPath);
            picture.MoveTo(ws.Cell(1, 1), 0, 0);
            // 4" × 1.03" at 96 DPI
            picture.WithSize((int)(4.0 * 96), (int)(1.03 * 96));
        }

        // ============================================================
        // 2. Day name — or frikandellen image on Friday
        // ============================================================
        if (planning.DayName.Equals("Vrijdag", StringComparison.OrdinalIgnoreCase))
        {
            var fridayImagePath = Path.Combine(_environment.WebRootPath, "frikandellen.png");
            if (File.Exists(fridayImagePath))
            {
                var picture = ws.AddPicture(fridayImagePath);
                picture.MoveTo(ws.Cell(1, 4), 0, 0);
                picture.Scale(0.5);
            }
            ws.Cell(5, 4).Value = "Frikandellen-Vrijdag";
            ws.Cell(5, 4).Style.Font.Bold = true;
            ws.Cell(5, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        else
        {
            var dayRange = ws.Range(3, 4, 4, 5); // D3:E4
            dayRange.Merge();
            ws.Cell(3, 4).Value = planning.DayName;
            ws.Cell(3, 4).Style.Font.FontSize = 28;
            ws.Cell(3, 4).Style.Font.Bold = true;
            dayRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            dayRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        // ============================================================
        // 3. Row 6 — headers: black bold text on colored backgrounds
        // ============================================================
        // Taken
        ws.Cell(6, ColTaken).Value = "Taken";
        ws.Cell(6, ColTaken).Style.Font.Bold = true;
        ws.Cell(6, ColTaken).Style.Font.FontColor = XLColor.Black;
        ws.Cell(6, ColTaken).Style.Fill.BackgroundColor = TealColor;
        ws.Cell(6, ColTaken).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        OuterBorder(ws.Range(6, ColTaken, 6, ColTaken));

        // Werknemers (merged B-F)
        var werkRange = ws.Range(6, ColWorkersStart, 6, ColWorkersEnd);
        werkRange.Merge();
        ws.Cell(6, ColWorkersStart).Value = "Werknemers";
        ws.Cell(6, ColWorkersStart).Style.Font.Bold = true;
        ws.Cell(6, ColWorkersStart).Style.Font.FontColor = XLColor.Black;
        ws.Cell(6, ColWorkersStart).Style.Fill.BackgroundColor = TealColor;
        werkRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        OuterBorder(werkRange);

        // Bijzonderheden (merged G-H)
        var bijzHdrRange = ws.Range(6, ColBijzG, 6, ColBijzH);
        bijzHdrRange.Merge();
        ws.Cell(6, ColBijzG).Value = "Bijzonderheden";
        ws.Cell(6, ColBijzG).Style.Font.Bold = true;
        ws.Cell(6, ColBijzG).Style.Font.FontColor = XLColor.Black;
        ws.Cell(6, ColBijzG).Style.Fill.BackgroundColor = TealColor;
        bijzHdrRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        OuterBorder(bijzHdrRange);

        // Afwezigen
        ws.Cell(6, ColAfwezig).Value = "Afwezigen";
        ws.Cell(6, ColAfwezig).Style.Font.Bold = true;
        ws.Cell(6, ColAfwezig).Style.Font.FontColor = XLColor.Black;
        ws.Cell(6, ColAfwezig).Style.Fill.BackgroundColor = OrangeColor;
        ws.Cell(6, ColAfwezig).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        OuterBorder(ws.Range(6, ColAfwezig, 6, ColAfwezig));

        // ============================================================
        // Build data: tasks chunked into rows of 5 workers each
        // ============================================================
        var dataEntries = new List<DataEntry>();
        const int workersPerRow = ColWorkersEnd - ColWorkersStart + 1; // 5

        foreach (var t in orderedTasks)
        {
            if (t.DividerAbove && dataEntries.Count > 0)
                dataEntries.Add(DataEntry.Separator);

            var workers = assignmentsByTask.GetValueOrDefault(t.Name, []);
            var chunks = workers.Chunk(workersPerRow).ToList();
            if (chunks.Count == 0)
                chunks.Add([]);

            for (int c = 0; c < chunks.Count; c++)
                dataEntries.Add(new DataEntry(t.Name, chunks[c], false, c == 0, c == 0 ? chunks.Count : 0));
        }

        // Total rows = enough for tasks and all absent workers
        var totalRows = Math.Max(dataEntries.Count, planning.AbsentWorkers.Count);

        // ============================================================
        // 6+7. Data rows — alternating white/gray at ROW level
        //       Cells after I to Z — white
        // ============================================================
        var dataStartRow = 7;
        var absentIdx = 0;
        var colorIndex = 0; // for alternating, resets across separator

        for (int i = 0; i < totalRows; i++)
        {
            var r = dataStartRow + i;
            var hasEntry = i < dataEntries.Count;
            var entry = hasEntry ? dataEntries[i] : null;
            var isSep = entry?.IsSeparator == true;

            if (isSep)
            {
                // Teal separator row across A-H — solid strip, no internal dividers
                var sepRange = ws.Range(r, ColTaken, r, ColBijzH);
                sepRange.Style.Fill.BackgroundColor = TealColor;
                sepRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                // Afwezigen on separator
                if (absentIdx < planning.AbsentWorkers.Count)
                    ws.Cell(r, ColAfwezig).Value = planning.AbsentWorkers[absentIdx++];
                colorIndex = 0; // reset alternating for right section
                continue;
            }

            var isFirst = entry?.IsFirstRow == true;
            if (isFirst || entry == null) colorIndex++;
            var rowBg = colorIndex % 2 == 0 ? XLColor.White : GrayColor;

            var workers = entry?.Workers ?? [];

            // Fill row A-H with alternating color
            for (int c = ColTaken; c <= ColBijzH; c++)
                ws.Cell(r, c).Style.Fill.BackgroundColor = rowBg;

            // Col A: Taken — only on first row, merged across continuation rows
            if (isFirst && entry != null)
            {
                var rowSpan = entry.RowSpan;

                if (rowSpan > 1)
                    ws.Range(r, ColTaken, r + rowSpan - 1, ColTaken).Merge();

                ws.Cell(r, ColTaken).Value = entry.TaskName;
                ws.Cell(r, ColTaken).Style.Font.Bold = true;
                ws.Cell(r, ColTaken).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(r, ColTaken).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                OuterBorder(ws.Range(r, ColTaken, r + rowSpan - 1, ColTaken));
            }
            else if (entry == null)
            {
                OuterBorder(ws.Range(r, ColTaken, r, ColTaken));
            }

            // Cols B-F: Werknemers
            for (int w = 0; w < workers.Count; w++)
                ws.Cell(r, ColWorkersStart + w).Value = workers[w];
            ws.Range(r, ColWorkersStart, r, ColWorkersEnd).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            // Cols G-H: Bijzonderheden
            OuterBorder(ws.Range(r, ColBijzG, r, ColBijzG));
            OuterBorder(ws.Range(r, ColBijzH, r, ColBijzH));

            // Col I: Afwezigen
            if (absentIdx < planning.AbsentWorkers.Count)
                ws.Cell(r, ColAfwezig).Value = planning.AbsentWorkers[absentIdx++];
        }

        var lastDataRow = dataStartRow + totalRows - 1;

        // ============================================================
        // 8. Afwezigen column — light orange fill, no internal borders
        // ============================================================
        var afwezigDataRange = ws.Range(dataStartRow, ColAfwezig, lastDataRow, ColAfwezig);
        afwezigDataRange.Style.Fill.BackgroundColor = LightOrangeColor;
        // Outside border around entire Afwezigen section (header + data)
        ws.Range(6, ColAfwezig, lastDataRow, ColAfwezig).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        // Header bottom border separates header from data
        OuterBorder(ws.Range(6, ColAfwezig, 6, ColAfwezig));

        // ============================================================
        // 9. Border line surrounding the entire "taken part" (table)
        // ============================================================
        ws.Range(6, ColTaken, lastDataRow, ColAfwezig).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        // ============================================================
        // 9. Opmerkingen — bordered box, 5 rows, white, no internal borders
        // ============================================================
        var opLabel = lastDataRow + 2; // skip one white row
        ws.Cell(opLabel, ColTaken).Value = "Opmerkingen:";
        ws.Cell(opLabel, ColTaken).Style.Font.Bold = true;

        var noteStart = opLabel + 1;
        var noteEnd = noteStart + 4; // 5 rows
        var noteRange = ws.Range(noteStart, ColTaken, noteEnd, ColBijzH);
        noteRange.Style.Fill.BackgroundColor = XLColor.White;
        noteRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        // ============================================================
        // Column widths
        // ============================================================
        ws.Column(ColTaken).Width = 25;
        for (int c = ColWorkersStart; c <= ColWorkersEnd; c++)
            ws.Column(c).Width = 22;
        ws.Column(ColBijzG).Width = 15;
        ws.Column(ColBijzH).Width = 28;
        ws.Column(ColAfwezig).Width = 25;

        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.FitToPages(1, 1);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private sealed record DataEntry(string? TaskName, IReadOnlyList<string> Workers, bool IsSeparator, bool IsFirstRow, int RowSpan)
    {
        public static readonly DataEntry Separator = new(null, [], true, false, 0);
    }

    private static void OuterBorder(IXLRange range)
    {
        range.Style.Border.TopBorder = XLBorderStyleValues.Thin;
        range.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        range.Style.Border.LeftBorder = XLBorderStyleValues.Thin;
        range.Style.Border.RightBorder = XLBorderStyleValues.Thin;
    }
}
