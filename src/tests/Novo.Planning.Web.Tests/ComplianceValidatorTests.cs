using FluentAssertions;
using Novo.Planning.Domain.Models;
using Novo.Planning.Web.Features.Planning.Services;

namespace Novo.Planning.Web.Tests;

public class ComplianceValidatorTests
{
    private readonly ComplianceValidator _validator = new();

    private static List<Person> CreateBasicPersons() =>
    [
        new() { Id = "p1", Name = "Alice", SpeaksDutch = true, SpeaksPolish = false,
            Skills = new() { ["TaskA"] = SkillLevel.Expert, ["TaskB"] = SkillLevel.Experienced } },
        new() { Id = "p2", Name = "Bob", SpeaksDutch = true, SpeaksPolish = false,
            Skills = new() { ["TaskA"] = SkillLevel.Experienced, ["TaskB"] = SkillLevel.Expert } },
    ];

    private static List<TaskDefinition> CreateBasicTasks() =>
    [
        new() { Id = "t1", Name = "TaskA", HeadcountRequired = 1, MinWorkersLevel1 = 0 },
        new() { Id = "t2", Name = "TaskB", HeadcountRequired = 1, MinWorkersLevel1 = 0 },
    ];

    [Fact]
    public void Validate_ValidPlanning_ReturnsNoViolations()
    {
        var planning = new PlanningModel
        {
            Assignments =
            [
                new() { TaskName = "TaskA", WorkerName = "Alice", SkillLevel = SkillLevel.Expert },
                new() { TaskName = "TaskB", WorkerName = "Bob", SkillLevel = SkillLevel.Expert },
            ]
        };

        var violations = _validator.Validate(planning, CreateBasicPersons(), CreateBasicTasks());
        violations.Should().BeEmpty();
    }

    [Fact]
    public void Validate_HeadcountMismatch_ReturnsError()
    {
        var planning = new PlanningModel
        {
            Assignments =
            [
                new() { TaskName = "TaskA", WorkerName = "Alice", SkillLevel = SkillLevel.Expert },
                // TaskB has 0 workers but requires 1
            ]
        };

        var violations = _validator.Validate(planning, CreateBasicPersons(), CreateBasicTasks());
        violations.Should().Contain(v => v.TaskName == "TaskB" && v.Severity == ViolationSeverity.Error);
    }

    [Fact]
    public void Validate_WorkerOverallocation_ReturnsError()
    {
        var planning = new PlanningModel
        {
            Assignments =
            [
                new() { TaskName = "TaskA", WorkerName = "Alice", SkillLevel = SkillLevel.Expert },
                new() { TaskName = "TaskB", WorkerName = "Alice", SkillLevel = SkillLevel.Experienced }, // duplicate
            ]
        };

        var violations = _validator.Validate(planning, CreateBasicPersons(), CreateBasicTasks());
        violations.Should().Contain(v => v.WorkerName == "Alice" && v.Severity == ViolationSeverity.Error);
    }

    [Fact]
    public void Validate_SkillLevel4Assignment_ReturnsError()
    {
        var persons = new List<Person>
        {
            new() { Id = "p1", Name = "Alice", SpeaksDutch = true,
                Skills = new() { ["TaskA"] = SkillLevel.Cannot } },
        };
        var tasks = new List<TaskDefinition>
        {
            new() { Id = "t1", Name = "TaskA", HeadcountRequired = 1 },
        };

        var planning = new PlanningModel
        {
            Assignments = [new() { TaskName = "TaskA", WorkerName = "Alice", SkillLevel = SkillLevel.Cannot }]
        };

        var violations = _validator.Validate(planning, persons, tasks);
        violations.Should().Contain(v => v.Severity == ViolationSeverity.Error && v.WorkerName == "Alice");
    }

    [Fact]
    public void Validate_LanguageIncompatibility_ReturnsWarning()
    {
        var persons = new List<Person>
        {
            new() { Id = "p1", Name = "Dutch", SpeaksDutch = true, SpeaksPolish = false,
                Skills = new() { ["Collab"] = SkillLevel.Expert } },
            new() { Id = "p2", Name = "Polish", SpeaksDutch = false, SpeaksPolish = true,
                Skills = new() { ["Collab"] = SkillLevel.Expert } },
        };
        var tasks = new List<TaskDefinition>
        {
            new() { Id = "t1", Name = "Collab", HeadcountRequired = 2, RequiresLanguageCollaboration = true },
        };

        var planning = new PlanningModel
        {
            Assignments =
            [
                new() { TaskName = "Collab", WorkerName = "Dutch", SkillLevel = SkillLevel.Expert },
                new() { TaskName = "Collab", WorkerName = "Polish", SkillLevel = SkillLevel.Expert },
            ]
        };

        var violations = _validator.Validate(planning, persons, tasks);
        violations.Should().Contain(v => v.Severity == ViolationSeverity.Warning && v.TaskName == "Collab");
    }

    [Fact]
    public void Validate_MinLevel1NotMet_ReturnsWarning()
    {
        var persons = new List<Person>
        {
            new() { Id = "p1", Name = "Alice", SpeaksDutch = true,
                Skills = new() { ["TaskA"] = SkillLevel.Beginner } },
        };
        var tasks = new List<TaskDefinition>
        {
            new() { Id = "t1", Name = "TaskA", HeadcountRequired = 1, MinWorkersLevel1 = 1 },
        };

        var planning = new PlanningModel
        {
            Assignments = [new() { TaskName = "TaskA", WorkerName = "Alice", SkillLevel = SkillLevel.Beginner }]
        };

        var violations = _validator.Validate(planning, persons, tasks);
        violations.Should().Contain(v => v.Severity == ViolationSeverity.Warning && v.TaskName == "TaskA");
    }
}
