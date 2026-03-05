using ClosedXML.Excel;
using Novo.Planning.Domain.Interfaces;
using Novo.Planning.Domain.Models;

namespace Novo.Planning.Infrastructure.Import;

public class ExcelImportService : IExcelImportService
{
    private readonly IPersonRepository _personRepository;
    private readonly ITaskDefinitionRepository _taskDefinitionRepository;

    private static readonly Dictionary<string, DayOfWeek> DutchDayMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["maandag"] = DayOfWeek.Monday,
        ["dinsdag"] = DayOfWeek.Tuesday,
        ["woensdag"] = DayOfWeek.Wednesday,
        ["donderdag"] = DayOfWeek.Thursday,
        ["vrijdag"] = DayOfWeek.Friday,
        ["zaterdag"] = DayOfWeek.Saturday,
        ["zondag"] = DayOfWeek.Sunday,
    };

    public ExcelImportService(IPersonRepository personRepository, ITaskDefinitionRepository taskDefinitionRepository)
    {
        _personRepository = personRepository;
        _taskDefinitionRepository = taskDefinitionRepository;
    }

    public async Task ImportAsync(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        await ImportFromWorkbookAsync(workbook);
    }

    public async Task ImportFromStreamAsync(Stream stream)
    {
        await UnloadAsync();

        using var workbook = new XLWorkbook(stream);
        await ImportFromWorkbookAsync(workbook);
    }

    public async Task UnloadAsync()
    {
        await _personRepository.ClearAsync();
        await _taskDefinitionRepository.ClearAsync();
    }

    private async Task ImportFromWorkbookAsync(XLWorkbook workbook)
    {
        var taskDefinitions = ImportTasks(workbook.Worksheet("Taken"));
        foreach (var task in taskDefinitions)
        {
            await _taskDefinitionRepository.UpsertAsync(task);
        }

        var taskNames = taskDefinitions.Select(t => t.Name).ToHashSet();
        var persons = ImportPersons(workbook.Worksheet("Werknemers"), taskNames);
        foreach (var person in persons)
        {
            await _personRepository.UpsertAsync(person);
        }

        // Import temp worker template as a special person
        var tempWorker = ImportTempWorkerTemplate(workbook.Worksheet("Uitzendkracht"), taskNames);
        if (tempWorker != null)
        {
            await _personRepository.UpsertAsync(tempWorker);
        }
    }

    private static List<TaskDefinition> ImportTasks(IXLWorksheet sheet)
    {
        var tasks = new List<TaskDefinition>();
        var headerRow = sheet.FirstRowUsed()!;
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int col = 1; col <= headerRow.LastCellUsed()!.Address.ColumnNumber; col++)
        {
            headers[headerRow.Cell(col).GetString().Trim()] = col;
        }

        var sortOrder = 0;
        foreach (var row in sheet.RowsUsed().Skip(1))
        {
            var name = row.Cell(headers["Taken"]).GetString().Trim();
            if (string.IsNullOrWhiteSpace(name)) continue;

            var task = new TaskDefinition
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                IsActive = GetIntValue(row, headers, "Aan") == 1,
                HeadcountRequired = GetIntValue(row, headers, "Aantal"),
                BoardPosition = GetIntValue(row, headers, "Verdeling oud planbord") == 2
                    ? BoardPosition.Right
                    : BoardPosition.Left,
                MinWorkersLevel1 = GetIntValue(row, headers, "Aantal_min_niveau_1"),
                MinWorkersLevel2 = GetIntValue(row, headers, "Aantal_min_niveau_2"),
                MinWorkersLevel3 = GetIntValue(row, headers, "Aantal_min_niveau_3"),
                RestLevel = (SkillLevel)GetIntValue(row, headers, "Rest_min_niveau", 3),
                RequiresLanguageCollaboration = GetIntValue(row, headers, "Samenwerken") == 1,
                SortOrder = sortOrder++
            };

            tasks.Add(task);
        }

        return tasks;
    }

    private static List<Person> ImportPersons(IXLWorksheet sheet, HashSet<string> taskNames)
    {
        var persons = new List<Person>();
        var headerRow = sheet.FirstRowUsed()!;
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int col = 1; col <= headerRow.LastCellUsed()!.Address.ColumnNumber; col++)
        {
            headers[headerRow.Cell(col).GetString().Trim()] = col;
        }

        foreach (var row in sheet.RowsUsed().Skip(1))
        {
            var name = row.Cell(headers["Werknemers"]).GetString().Trim();
            if (string.IsNullOrWhiteSpace(name)) continue;

            var person = new Person
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                SpeaksDutch = GetIntValue(row, headers, "Nederlands") == 1,
                SpeaksPolish = GetIntValue(row, headers, "Pools") == 1,
                DefaultDaysOff = ParseDaysOff(row.Cell(headers["Vrije dagen"]).GetString()),
                Skills = ParseSkills(row, headers, taskNames)
            };

            persons.Add(person);
        }

        return persons;
    }

    private static Person? ImportTempWorkerTemplate(IXLWorksheet sheet, HashSet<string> taskNames)
    {
        var headerRow = sheet.FirstRowUsed()!;
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int col = 1; col <= headerRow.LastCellUsed()!.Address.ColumnNumber; col++)
        {
            headers[headerRow.Cell(col).GetString().Trim()] = col;
        }

        var dataRow = sheet.RowsUsed().Skip(1).FirstOrDefault();
        if (dataRow == null) return null;

        return new Person
        {
            Id = "temp-worker-template",
            Name = "Uitzendkracht",
            SpeaksDutch = GetIntValue(dataRow, headers, "Nederlands") == 1,
            SpeaksPolish = GetIntValue(dataRow, headers, "Pools") == 1,
            DefaultDaysOff = [],
            Skills = ParseSkills(dataRow, headers, taskNames)
        };
    }

    private static Dictionary<string, SkillLevel> ParseSkills(IXLRow row, Dictionary<string, int> headers, HashSet<string> taskNames)
    {
        var skills = new Dictionary<string, SkillLevel>();
        foreach (var taskName in taskNames)
        {
            if (headers.TryGetValue(taskName, out var col))
            {
                var value = GetIntValue(row, col, 4);
                skills[taskName] = (SkillLevel)Math.Clamp(value, 1, 4);
            }
        }
        return skills;
    }

    private static HashSet<DayOfWeek> ParseDaysOff(string? value)
    {
        var result = new HashSet<DayOfWeek>();
        if (string.IsNullOrWhiteSpace(value)) return result;

        var days = value.Replace(",", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var day in days)
        {
            if (DutchDayMap.TryGetValue(day.Trim(), out var dayOfWeek))
            {
                result.Add(dayOfWeek);
            }
        }
        return result;
    }

    private static int GetIntValue(IXLRow row, Dictionary<string, int> headers, string columnName, int defaultValue = 0)
    {
        if (!headers.TryGetValue(columnName, out var col)) return defaultValue;
        return GetIntValue(row, col, defaultValue);
    }

    private static int GetIntValue(IXLRow row, int col, int defaultValue = 0)
    {
        var cell = row.Cell(col);
        if (cell.IsEmpty()) return defaultValue;
        if (cell.TryGetValue<double>(out var doubleVal)) return (int)doubleVal;
        if (int.TryParse(cell.GetString(), out var intVal)) return intVal;
        return defaultValue;
    }
}
