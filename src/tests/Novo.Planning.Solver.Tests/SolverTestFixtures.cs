using Novo.Planning.Domain.Models;

namespace Novo.Planning.Solver.Tests;

/// <summary>
/// Shared test fixtures providing realistic sample data.
/// Port of conftest.py.
/// </summary>
public static class SolverTestFixtures
{
    // --- Small scale (3 workers, 2 tasks) ---

    public static readonly string[] SmallTaskNames = ["Ompakken", "Krimpen"];

    public static List<Person> CreateSmallWorkers() =>
    [
        new Person
        {
            Id = "alice", Name = "Alice",
            SpeaksDutch = true, SpeaksPolish = false,
            Skills = new Dictionary<string, SkillLevel>
            {
                ["Ompakken"] = SkillLevel.Expert,    // 1
                ["Krimpen"] = SkillLevel.Experienced, // 2
            }
        },
        new Person
        {
            Id = "bob", Name = "Bob",
            SpeaksDutch = true, SpeaksPolish = false,
            Skills = new Dictionary<string, SkillLevel>
            {
                ["Ompakken"] = SkillLevel.Experienced, // 2
                ["Krimpen"] = SkillLevel.Expert,        // 1
            }
        },
        new Person
        {
            Id = "charlie", Name = "Charlie",
            SpeaksDutch = false, SpeaksPolish = true,
            Skills = new Dictionary<string, SkillLevel>
            {
                ["Ompakken"] = SkillLevel.Beginner,    // 3
                ["Krimpen"] = SkillLevel.Experienced,  // 2
            }
        },
    ];

    public static List<TaskDefinition> CreateSmallTasks() =>
    [
        new TaskDefinition
        {
            Id = "ompakken", Name = "Ompakken",
            IsActive = true, HeadcountRequired = 1,
            BoardPosition = BoardPosition.Left,
            MinWorkersLevel1 = 1, MinWorkersLevel2 = 0, MinWorkersLevel3 = 0,
            RestLevel = SkillLevel.Beginner,
            RequiresLanguageCollaboration = false,
            SortOrder = 0,
        },
        new TaskDefinition
        {
            Id = "krimpen", Name = "Krimpen",
            IsActive = true, HeadcountRequired = 2,
            BoardPosition = BoardPosition.Right,
            MinWorkersLevel1 = 0, MinWorkersLevel2 = 1, MinWorkersLevel3 = 0,
            RestLevel = SkillLevel.Beginner,
            RequiresLanguageCollaboration = false,
            SortOrder = 1,
        },
    ];

    // --- Production scale (20 workers, 12 tasks) ---

    public static readonly string[] ProductionTaskNames =
    [
        "Pallets wassen", "Ompakken", "Lijmen deur 11", "Warehousing",
        "Vrachtwagen 79-BKX-1", "Vrachtwagen BR-NR-81", "Locatie Extern",
        "Voorman golfkarton", "Kratjes wikkelen", "Stansmachine 1",
        "Schneider hoekstukken", "Plotter",
    ];

    public static List<Person> CreateProductionWorkers()
    {
        //                                Name          NL    PL   PW  OM  L11 WAR V79 VBR LOC VGK KW  ST1 SCH PLO
        (string Name, bool NL, bool PL, int[] Skills)[] data =
        [
            ("Worker_01", true,  false, [4, 4, 4, 1, 4, 4, 4, 4, 3, 4, 4, 4]),
            ("Worker_02", true,  false, [4, 4, 4, 4, 2, 1, 4, 4, 3, 4, 4, 4]),
            ("Worker_03", true,  false, [3, 4, 4, 2, 4, 4, 4, 1, 3, 4, 4, 4]),
            ("Worker_04", true,  false, [4, 4, 4, 1, 4, 4, 2, 2, 4, 4, 4, 4]),
            ("Worker_05", true,  false, [4, 4, 4, 1, 4, 4, 1, 4, 2, 4, 4, 4]),
            ("Worker_06", true,  false, [4, 3, 1, 4, 4, 4, 4, 4, 4, 4, 4, 4]),
            ("Worker_07", true,  false, [4, 2, 1, 4, 4, 4, 4, 4, 3, 3, 3, 4]),
            ("Worker_08", false, true,  [3, 1, 2, 3, 4, 4, 4, 4, 2, 4, 4, 4]),
            ("Worker_09", true,  false, [1, 4, 4, 3, 4, 4, 4, 4, 1, 4, 4, 4]),
            ("Worker_10", true,  false, [4, 3, 3, 4, 4, 4, 4, 4, 4, 1, 2, 1]),
            ("Worker_11", true,  false, [4, 3, 3, 4, 4, 4, 4, 4, 4, 1, 2, 2]),
            ("Worker_12", true,  true,  [4, 1, 2, 4, 4, 4, 4, 4, 4, 2, 2, 4]),
            ("Worker_13", true,  true,  [4, 1, 2, 4, 4, 4, 4, 4, 4, 2, 2, 4]),
            ("Worker_14", true,  false, [4, 2, 1, 4, 4, 4, 4, 4, 4, 3, 3, 4]),
            ("Worker_15", true,  false, [4, 2, 2, 4, 4, 4, 4, 4, 4, 2, 2, 2]),
            ("Worker_16", true,  false, [4, 2, 3, 4, 4, 4, 4, 4, 4, 1, 1, 4]),
            ("Worker_17", false, true,  [4, 1, 3, 4, 4, 4, 4, 4, 4, 4, 1, 4]),
            ("Worker_18", true,  false, [4, 4, 4, 4, 1, 2, 4, 4, 3, 4, 4, 4]),
            ("Worker_19", false, true,  [4, 3, 3, 4, 4, 4, 4, 4, 4, 2, 1, 4]),
            ("Worker_20", true,  false, [4, 3, 2, 4, 4, 4, 4, 4, 4, 2, 2, 4]),
        ];

        return data.Select((d, i) =>
        {
            var skills = new Dictionary<string, SkillLevel>();
            for (int t = 0; t < ProductionTaskNames.Length; t++)
            {
                skills[ProductionTaskNames[t]] = (SkillLevel)d.Skills[t];
            }
            return new Person
            {
                Id = $"worker-{i + 1:D2}",
                Name = d.Name,
                SpeaksDutch = d.NL,
                SpeaksPolish = d.PL,
                Skills = skills,
            };
        }).ToList();
    }

    public static List<TaskDefinition> CreateProductionTasks()
    {
        int[] headcounts =              [1, 5, 3, 2, 1, 1, 1, 1, 1, 1, 2, 1];
        int[] boardPositions =          [1, 1, 1, 1, 1, 1, 1, 2, 1, 2, 2, 2];
        int[] minLevel1 =               [1, 1, 2, 1, 1, 1, 0, 0, 0, 1, 0, 0];
        int[] minLevel2 =               [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        int[] minLevel3 =               [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        int[] samenwerken =             [0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0];

        return ProductionTaskNames.Select((name, i) => new TaskDefinition
        {
            Id = $"task-{i + 1:D2}",
            Name = name,
            IsActive = true,
            HeadcountRequired = headcounts[i],
            BoardPosition = (BoardPosition)boardPositions[i],
            MinWorkersLevel1 = minLevel1[i],
            MinWorkersLevel2 = minLevel2[i],
            MinWorkersLevel3 = minLevel3[i],
            RestLevel = SkillLevel.Beginner,
            RequiresLanguageCollaboration = samenwerken[i] == 1,
            SortOrder = i,
        }).ToList();
    }
}
