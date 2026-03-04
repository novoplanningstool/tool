using ClosedXML.Excel;
using Novo.Planning.Domain.Interfaces;
using Novo.Planning.Domain.Models;

namespace Novo.Planning.Web.Features.Planning.Services;

public class ExcelExportService : IExcelExportService
{
    private static readonly XLColor TealColor = XLColor.FromHtml("#59B6AD");
    private static readonly XLColor OrangeColor = XLColor.FromHtml("#FF6103");
    private static readonly XLColor LightBlueColor = XLColor.FromHtml("#d2e1e9");
    private static readonly XLColor WhiteColor = XLColor.FromHtml("#FFFFFF");

    private readonly ITaskDefinitionRepository _taskRepository;

    public ExcelExportService(ITaskDefinitionRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public byte[] Export(PlanningModel planning)
    {
        // Load task definitions to determine board positions
        var tasks = _taskRepository.GetAllAsync().GetAwaiter().GetResult();
        var taskBoardPositions = tasks.ToDictionary(t => t.Name, t => t.BoardPosition);

        using var workbook = new XLWorkbook();

        var sheetName = string.IsNullOrWhiteSpace(planning.TemplateName)
            ? planning.Date.ToString("yyyy-MM-dd")
            : planning.TemplateName;

        var worksheet = workbook.Worksheets.Add(sheetName);

        var currentRow = 1;

        // Title row with NOVO branding
        currentRow = WriteTitle(worksheet, currentRow, planning);

        // Strategy info
        currentRow = WriteStrategyInfo(worksheet, currentRow, planning);

        // Blank separator row
        currentRow++;

        // Left board section (BoardPosition.Left)
        var leftAssignments = planning.Assignments
            .Where(a => !string.IsNullOrEmpty(a.TaskName))
            .GroupBy(a => a.TaskName)
            .Where(g => GetBoardPosition(g.Key, taskBoardPositions) == BoardPosition.Left)
            .OrderBy(g => g.Key)
            .ToList();

        if (leftAssignments.Count > 0)
        {
            currentRow = WriteBoardSection(worksheet, currentRow, "Links", leftAssignments);
            currentRow++;
        }

        // Right board section (BoardPosition.Right)
        var rightAssignments = planning.Assignments
            .Where(a => !string.IsNullOrEmpty(a.TaskName))
            .GroupBy(a => a.TaskName)
            .Where(g => GetBoardPosition(g.Key, taskBoardPositions) == BoardPosition.Right)
            .OrderBy(g => g.Key)
            .ToList();

        if (rightAssignments.Count > 0)
        {
            currentRow = WriteBoardSection(worksheet, currentRow, "Rechts", rightAssignments);
            currentRow++;
        }

        // Absent workers section
        if (planning.AbsentWorkers.Count > 0)
        {
            currentRow = WriteAbsentWorkers(worksheet, currentRow, planning.AbsentWorkers);
            currentRow++;
        }

        // Warnings section
        if (planning.Warnings.Count > 0)
        {
            currentRow = WriteWarnings(worksheet, currentRow, planning.Warnings);
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static BoardPosition GetBoardPosition(
        string taskName,
        Dictionary<string, BoardPosition> taskBoardPositions)
    {
        return taskBoardPositions.TryGetValue(taskName, out var position)
            ? position
            : BoardPosition.Left; // default to left if task definition not found
    }

    private static int WriteTitle(IXLWorksheet worksheet, int row, PlanningModel planning)
    {
        var titleText = planning.IsTemplate
            ? $"NOVO Planning - {planning.TemplateName}"
            : $"NOVO Planning - {planning.DayName} {planning.Date:dd-MM-yyyy}";

        var cell = worksheet.Cell(row, 1);
        cell.Value = titleText;
        cell.Style.Font.Bold = true;
        cell.Style.Font.FontSize = 16;
        cell.Style.Font.FontColor = XLColor.White;

        var titleRange = worksheet.Range(row, 1, row, 4);
        titleRange.Merge();
        titleRange.Style.Fill.BackgroundColor = TealColor;
        titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        return row + 1;
    }

    private static int WriteStrategyInfo(IXLWorksheet worksheet, int row, PlanningModel planning)
    {
        var strategyText = planning.Strategy switch
        {
            OptimizationStrategy.MaximizeExpertise => "Maximaliseer Expertise",
            OptimizationStrategy.LearningFocused => "Leergericht",
            OptimizationStrategy.Hybrid => "Hybride",
            _ => planning.Strategy.ToString()
        };

        var cell = worksheet.Cell(row, 1);
        cell.Value = $"Strategie: {strategyText}";
        cell.Style.Font.Italic = true;
        cell.Style.Font.FontColor = XLColor.FromHtml("#666666");

        return row + 1;
    }

    private static int WriteBoardSection(
        IXLWorksheet worksheet,
        int row,
        string sectionTitle,
        List<IGrouping<string, PlanningAssignment>> taskGroups)
    {
        // Section header
        var headerCell = worksheet.Cell(row, 1);
        headerCell.Value = sectionTitle;
        headerCell.Style.Font.Bold = true;
        headerCell.Style.Font.FontSize = 13;
        headerCell.Style.Font.FontColor = XLColor.White;

        var headerRange = worksheet.Range(row, 1, row, 4);
        headerRange.Merge();
        headerRange.Style.Fill.BackgroundColor = OrangeColor;

        row++;

        // Column headers
        var colHeaderRow = worksheet.Range(row, 1, row, 3);
        worksheet.Cell(row, 1).Value = "Taak";
        worksheet.Cell(row, 2).Value = "Medewerker";
        worksheet.Cell(row, 3).Value = "Niveau";

        colHeaderRow.Style.Font.Bold = true;
        colHeaderRow.Style.Fill.BackgroundColor = TealColor;
        colHeaderRow.Style.Font.FontColor = XLColor.White;

        row++;

        // Data rows with alternating colors
        var rowIndex = 0;
        foreach (var taskGroup in taskGroups)
        {
            var isFirstInGroup = true;
            foreach (var assignment in taskGroup.OrderBy(a => a.SkillLevel))
            {
                var bgColor = rowIndex % 2 == 0 ? WhiteColor : LightBlueColor;

                worksheet.Cell(row, 1).Value = isFirstInGroup ? assignment.TaskName : string.Empty;
                worksheet.Cell(row, 2).Value = assignment.WorkerName;
                worksheet.Cell(row, 3).Value = FormatSkillLevel(assignment.SkillLevel);

                var dataRange = worksheet.Range(row, 1, row, 3);
                dataRange.Style.Fill.BackgroundColor = bgColor;

                isFirstInGroup = false;
                row++;
                rowIndex++;
            }
        }

        return row;
    }

    private static int WriteAbsentWorkers(IXLWorksheet worksheet, int row, List<string> absentWorkers)
    {
        var headerCell = worksheet.Cell(row, 1);
        headerCell.Value = "Afwezige medewerkers";
        headerCell.Style.Font.Bold = true;
        headerCell.Style.Font.FontSize = 13;
        headerCell.Style.Font.FontColor = XLColor.White;

        var headerRange = worksheet.Range(row, 1, row, 4);
        headerRange.Merge();
        headerRange.Style.Fill.BackgroundColor = OrangeColor;

        row++;

        for (int i = 0; i < absentWorkers.Count; i++)
        {
            var bgColor = i % 2 == 0 ? WhiteColor : LightBlueColor;
            var cell = worksheet.Cell(row, 1);
            cell.Value = absentWorkers[i];

            var dataRange = worksheet.Range(row, 1, row, 3);
            dataRange.Style.Fill.BackgroundColor = bgColor;

            row++;
        }

        return row;
    }

    private static int WriteWarnings(IXLWorksheet worksheet, int row, List<string> warnings)
    {
        var headerCell = worksheet.Cell(row, 1);
        headerCell.Value = "Waarschuwingen";
        headerCell.Style.Font.Bold = true;
        headerCell.Style.Font.FontSize = 13;
        headerCell.Style.Font.FontColor = XLColor.White;

        var headerRange = worksheet.Range(row, 1, row, 4);
        headerRange.Merge();
        headerRange.Style.Fill.BackgroundColor = OrangeColor;

        row++;

        foreach (var warning in warnings)
        {
            var cell = worksheet.Cell(row, 1);
            cell.Value = warning;
            cell.Style.Font.FontColor = XLColor.Red;

            var warningRange = worksheet.Range(row, 1, row, 4);
            warningRange.Merge();

            row++;
        }

        return row;
    }

    private static string FormatSkillLevel(SkillLevel level)
    {
        return level switch
        {
            SkillLevel.Expert => "1 - Expert",
            SkillLevel.Experienced => "2 - Experienced",
            SkillLevel.Beginner => "3 - Beginner",
            SkillLevel.Cannot => "4 - Cannot",
            _ => level.ToString()
        };
    }
}
