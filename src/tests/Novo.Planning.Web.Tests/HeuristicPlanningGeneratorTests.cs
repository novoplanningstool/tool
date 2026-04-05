using FluentAssertions;
using Novo.Planning.Domain.Models;
using Novo.Planning.Web.Features.Planning.Services;

namespace Novo.Planning.Web.Tests;

public class HeuristicPlanningGeneratorTests
{
    private readonly HeuristicPlanningGenerator _generator = new();

    [Fact]
    public void Generate_AssignsWorkersToTasks()
    {
        var workers = new List<Person>
        {
            new() { Name = "Alice", Skills = new() { ["TaskA"] = SkillLevel.Expert } },
            new() { Name = "Bob", Skills = new() { ["TaskA"] = SkillLevel.Experienced } },
        };
        var tasks = new List<TaskDefinition>
        {
            new() { Name = "TaskA", HeadcountRequired = 2 },
        };

        var assignments = _generator.Generate(workers, tasks);

        assignments.Should().HaveCount(2);
        assignments.Select(a => a.WorkerName).Should().BeEquivalentTo(["Alice", "Bob"]);
        assignments.Should().OnlyContain(a => a.TaskName == "TaskA");
    }

    [Fact]
    public void Generate_ExcludesCannotWorkers()
    {
        var workers = new List<Person>
        {
            new() { Name = "Alice", Skills = new() { ["TaskA"] = SkillLevel.Expert } },
            new() { Name = "Bob", Skills = new() { ["TaskA"] = SkillLevel.Cannot } },
        };
        var tasks = new List<TaskDefinition>
        {
            new() { Name = "TaskA", HeadcountRequired = 1 },
        };

        var assignments = _generator.Generate(workers, tasks);

        assignments.Should().HaveCount(1);
        assignments[0].WorkerName.Should().Be("Alice");
    }

    [Fact]
    public void Generate_FillsMinWorkersLevel1WithExperts()
    {
        var workers = new List<Person>
        {
            new() { Name = "Expert", Skills = new() { ["TaskA"] = SkillLevel.Expert } },
            new() { Name = "Beginner", Skills = new() { ["TaskA"] = SkillLevel.Beginner } },
        };
        var tasks = new List<TaskDefinition>
        {
            new() { Name = "TaskA", HeadcountRequired = 2, MinWorkersLevel1 = 1 },
        };

        var assignments = _generator.Generate(workers, tasks);

        assignments.Should().HaveCount(2);
        assignments.Should().Contain(a => a.WorkerName == "Expert" && a.SkillLevel == SkillLevel.Expert);
    }

    [Fact]
    public void Generate_FillsMinWorkersLevel2()
    {
        var workers = new List<Person>
        {
            new() { Name = "Experienced", Skills = new() { ["TaskA"] = SkillLevel.Experienced } },
            new() { Name = "Beginner", Skills = new() { ["TaskA"] = SkillLevel.Beginner } },
        };
        var tasks = new List<TaskDefinition>
        {
            new() { Name = "TaskA", HeadcountRequired = 2, MinWorkersLevel2 = 1 },
        };

        var assignments = _generator.Generate(workers, tasks);

        assignments.Should().HaveCount(2);
        assignments.Should().Contain(a => a.WorkerName == "Experienced" && a.SkillLevel == SkillLevel.Experienced);
    }

    [Fact]
    public void Generate_PrefersRegularWorkersOverTemp()
    {
        var workers = new List<Person>
        {
            new() { Id = $"{WellKnownIds.TempWorkerIdPrefix}1", Name = "Temp", Skills = new() { ["TaskA"] = SkillLevel.Expert } },
            new() { Name = "Regular", Skills = new() { ["TaskA"] = SkillLevel.Expert } },
        };
        var tasks = new List<TaskDefinition>
        {
            new() { Name = "TaskA", HeadcountRequired = 1 },
        };

        var assignments = _generator.Generate(workers, tasks);

        assignments.Should().HaveCount(1);
        assignments[0].WorkerName.Should().Be("Regular");
    }

    [Fact]
    public void Generate_DoesNotAssignWorkerToMultipleTasks()
    {
        var workers = new List<Person>
        {
            new() { Name = "Alice", Skills = new() { ["TaskA"] = SkillLevel.Expert, ["TaskB"] = SkillLevel.Expert } },
            new() { Name = "Bob", Skills = new() { ["TaskA"] = SkillLevel.Experienced, ["TaskB"] = SkillLevel.Experienced } },
        };
        var tasks = new List<TaskDefinition>
        {
            new() { Name = "TaskA", HeadcountRequired = 1 },
            new() { Name = "TaskB", HeadcountRequired = 1 },
        };

        var assignments = _generator.Generate(workers, tasks);

        assignments.Should().HaveCount(2);
        var workerNames = assignments.Select(a => a.WorkerName).ToList();
        workerNames.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Generate_HandlesInsufficientWorkers()
    {
        var workers = new List<Person>
        {
            new() { Name = "Alice", Skills = new() { ["TaskA"] = SkillLevel.Expert } },
        };
        var tasks = new List<TaskDefinition>
        {
            new() { Name = "TaskA", HeadcountRequired = 3 },
        };

        var assignments = _generator.Generate(workers, tasks);

        // Only 1 worker available, can't fill headcount of 3
        assignments.Should().HaveCount(1);
    }

    [Fact]
    public void Generate_EmptyWorkers_ReturnsEmpty()
    {
        var tasks = new List<TaskDefinition>
        {
            new() { Name = "TaskA", HeadcountRequired = 2 },
        };

        var assignments = _generator.Generate([], tasks);

        assignments.Should().BeEmpty();
    }

    [Fact]
    public void Generate_EmptyTasks_ReturnsEmpty()
    {
        var workers = new List<Person>
        {
            new() { Name = "Alice", Skills = new() { ["TaskA"] = SkillLevel.Expert } },
        };

        var assignments = _generator.Generate(workers, []);

        assignments.Should().BeEmpty();
    }

    [Fact]
    public void Generate_LanguageCollaboration_TriesToMatchLanguage()
    {
        // 3 Dutch speakers + 1 Polish speaker, task needs 3 and requires language collab
        var workers = new List<Person>
        {
            new() { Name = "Dutch1", SpeaksDutch = true, SpeaksPolish = false,
                Skills = new() { ["Collab"] = SkillLevel.Expert } },
            new() { Name = "Dutch2", SpeaksDutch = true, SpeaksPolish = false,
                Skills = new() { ["Collab"] = SkillLevel.Experienced } },
            new() { Name = "Polish", SpeaksDutch = false, SpeaksPolish = true,
                Skills = new() { ["Collab"] = SkillLevel.Expert } },
            new() { Name = "Dutch3", SpeaksDutch = true, SpeaksPolish = false,
                Skills = new() { ["Collab"] = SkillLevel.Beginner } },
        };
        var tasks = new List<TaskDefinition>
        {
            new() { Name = "Collab", HeadcountRequired = 3, RequiresLanguageCollaboration = true },
        };

        var assignments = _generator.Generate(workers, tasks);

        assignments.Should().HaveCount(3);
        // Should prefer all-Dutch team since majority is Dutch
        var assignedWorkers = assignments.Select(a => a.WorkerName).ToList();
        assignedWorkers.Should().NotContain("Polish");
    }

    [Fact]
    public void Generate_SetsCorrectSkillLevel()
    {
        var workers = new List<Person>
        {
            new() { Name = "Alice", Skills = new() { ["TaskA"] = SkillLevel.Experienced } },
        };
        var tasks = new List<TaskDefinition>
        {
            new() { Name = "TaskA", HeadcountRequired = 1 },
        };

        var assignments = _generator.Generate(workers, tasks);

        assignments.Should().HaveCount(1);
        assignments[0].SkillLevel.Should().Be(SkillLevel.Experienced);
    }
}
