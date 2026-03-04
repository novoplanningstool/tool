using FluentAssertions;
using Novo.Planning.Domain.Models;

namespace Novo.Planning.Solver.Tests;

/// <summary>
/// Port of TestBuildAndSolve from test_solver.py.
/// </summary>
public class SolverServiceTests
{
    private readonly ISolverService _solver = new SolverService();
    private readonly List<Person> _workers = SolverTestFixtures.CreateSmallWorkers();
    private readonly List<TaskDefinition> _tasks = SolverTestFixtures.CreateSmallTasks();

    [Fact]
    public void Solve_MaximizeExpertise_ReturnsOptimal()
    {
        var result = _solver.Solve(_workers, _tasks, OptimizationStrategy.MaximizeExpertise);

        result.Status.Should().Be(SolverStatus.Optimal);
    }

    [Fact]
    public void Solve_EachWorkerAssignedOnce()
    {
        var result = _solver.Solve(_workers, _tasks, OptimizationStrategy.MaximizeExpertise);

        var workerNames = result.Assignments.Select(a => a.WorkerName).ToList();
        workerNames.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Solve_TaskHeadcountSatisfied()
    {
        var result = _solver.Solve(_workers, _tasks, OptimizationStrategy.MaximizeExpertise);

        var ompakkenCount = result.Assignments.Count(a => a.TaskName == "Ompakken");
        var krimpenCount = result.Assignments.Count(a => a.TaskName == "Krimpen");

        ompakkenCount.Should().Be(1);
        krimpenCount.Should().Be(2);
    }

    [Theory]
    [InlineData(OptimizationStrategy.MaximizeExpertise)]
    [InlineData(OptimizationStrategy.LearningFocused)]
    [InlineData(OptimizationStrategy.Hybrid)]
    public void Solve_AllStrategies_ProduceOptimal(OptimizationStrategy strategy)
    {
        var result = _solver.Solve(_workers, _tasks, strategy);

        result.Status.Should().Be(SolverStatus.Optimal, $"Strategy {strategy} should produce optimal");
    }

    [Fact]
    public void Solve_Infeasible_WithLevel4OnlyWorker()
    {
        var workers = new List<Person>
        {
            new()
            {
                Id = "alice", Name = "Alice",
                SpeaksDutch = true, SpeaksPolish = false,
                Skills = new Dictionary<string, SkillLevel> { ["TaskA"] = SkillLevel.Expert }
            },
            new()
            {
                Id = "onlyfour", Name = "OnlyFour",
                SpeaksDutch = true, SpeaksPolish = false,
                Skills = new Dictionary<string, SkillLevel> { ["TaskA"] = SkillLevel.Cannot }
            },
        };
        var tasks = new List<TaskDefinition>
        {
            new()
            {
                Id = "taska", Name = "TaskA",
                IsActive = true, HeadcountRequired = 2,
                BoardPosition = BoardPosition.Left,
                MinWorkersLevel1 = 0, MinWorkersLevel2 = 0, MinWorkersLevel3 = 0,
                RestLevel = SkillLevel.Beginner,
            }
        };

        var result = _solver.Solve(workers, tasks, OptimizationStrategy.MaximizeExpertise);

        result.Status.Should().Be(SolverStatus.Infeasible);
        result.Level4OnlyWorkers.Should().Contain("OnlyFour");
    }

    [Fact]
    public void Solve_Relaxation_RemovesLanguageConstraint()
    {
        var workers = new List<Person>
        {
            new()
            {
                Id = "dutch", Name = "Dutch",
                SpeaksDutch = true, SpeaksPolish = false,
                Skills = new Dictionary<string, SkillLevel> { ["CollabTask"] = SkillLevel.Expert }
            },
            new()
            {
                Id = "polish", Name = "Polish",
                SpeaksDutch = false, SpeaksPolish = true,
                Skills = new Dictionary<string, SkillLevel> { ["CollabTask"] = SkillLevel.Expert }
            },
        };
        var tasks = new List<TaskDefinition>
        {
            new()
            {
                Id = "collab", Name = "CollabTask",
                IsActive = true, HeadcountRequired = 2,
                BoardPosition = BoardPosition.Left,
                MinWorkersLevel1 = 0, MinWorkersLevel2 = 0, MinWorkersLevel3 = 0,
                RestLevel = SkillLevel.Beginner,
                RequiresLanguageCollaboration = true,
            }
        };

        var result = _solver.Solve(workers, tasks, OptimizationStrategy.MaximizeExpertise);

        result.Status.Should().Be(SolverStatus.Relaxed);
        result.RelaxationAttempt.Should().BeGreaterThanOrEqualTo(1);
        result.Warnings.Should().NotBeEmpty();
    }
}

/// <summary>
/// Port of TestSolveResultDefaults from test_solver.py.
/// </summary>
public class SolverResultDefaultsTests
{
    [Fact]
    public void SolverResult_HasCorrectDefaults()
    {
        var result = new SolverResult { Status = SolverStatus.Optimal };

        result.Warnings.Should().BeEmpty();
        result.Level4OnlyWorkers.Should().BeEmpty();
        result.Level4OnlyTasks.Should().BeEmpty();
        result.RelaxationAttempt.Should().Be(0);
    }
}
