using FluentAssertions;
using Novo.Planning.Domain.Models;

namespace Novo.Planning.Solver.Tests;

/// <summary>
/// Port of TestProductionScaleSolver from test_solver.py.
/// Integration tests using a realistic 20-worker / 12-task scenario.
/// </summary>
public class ProductionScaleSolverTests
{
    private readonly ISolverService _solver = new SolverService();
    private readonly List<Person> _workers = SolverTestFixtures.CreateProductionWorkers();
    private readonly List<TaskDefinition> _tasks = SolverTestFixtures.CreateProductionTasks();

    public static TheoryData<OptimizationStrategy> AllStrategies => new()
    {
        OptimizationStrategy.MaximizeExpertise,
        OptimizationStrategy.LearningFocused,
        OptimizationStrategy.Hybrid,
    };

    [Theory]
    [MemberData(nameof(AllStrategies))]
    public void Solve_SolvesOptimal(OptimizationStrategy strategy)
    {
        var result = _solver.Solve(_workers, _tasks, strategy);

        result.Status.Should().Be(SolverStatus.Optimal, $"Expected optimal, got {result.Status}");
        result.RelaxationAttempt.Should().Be(0);
        result.Warnings.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(AllStrategies))]
    public void Solve_EachWorkerAssignedExactlyOnce(OptimizationStrategy strategy)
    {
        var result = _solver.Solve(_workers, _tasks, strategy);

        var workerNames = result.Assignments.Select(a => a.WorkerName).ToList();
        workerNames.Should().HaveCount(20, "Expected 20 assignments");
        workerNames.Should().OnlyHaveUniqueItems("Duplicate worker assignments found");
    }

    [Theory]
    [MemberData(nameof(AllStrategies))]
    public void Solve_TaskHeadcountSatisfied(OptimizationStrategy strategy)
    {
        var result = _solver.Solve(_workers, _tasks, strategy);

        foreach (var task in _tasks)
        {
            var count = result.Assignments.Count(a => a.TaskName == task.Name);
            count.Should().Be(task.HeadcountRequired,
                $"{task.Name}: expected {task.HeadcountRequired} workers, got {count}");
        }
    }

    [Theory]
    [MemberData(nameof(AllStrategies))]
    public void Solve_SkillEligibilityRespected(OptimizationStrategy strategy)
    {
        var result = _solver.Solve(_workers, _tasks, strategy);

        foreach (var assignment in result.Assignments)
        {
            var worker = _workers.First(w => w.Name == assignment.WorkerName);
            var actualSkill = worker.Skills[assignment.TaskName];
            ((int)actualSkill).Should().BeLessThanOrEqualTo((int)assignment.SkillLevel,
                $"Worker {worker.Name} has skill {actualSkill} on {assignment.TaskName}, " +
                $"but was assigned at level {assignment.SkillLevel}");
        }
    }

    [Theory]
    [MemberData(nameof(AllStrategies))]
    public void Solve_LanguageCompatibilityOnWarehousing(OptimizationStrategy strategy)
    {
        var result = _solver.Solve(_workers, _tasks, strategy);

        var warehousingWorkers = result.Assignments
            .Where(a => a.TaskName == "Warehousing")
            .Select(a => _workers.First(w => w.Name == a.WorkerName))
            .ToList();

        for (int i = 0; i < warehousingWorkers.Count; i++)
        {
            for (int j = i + 1; j < warehousingWorkers.Count; j++)
            {
                var w1 = warehousingWorkers[i];
                var w2 = warehousingWorkers[j];
                var nlShared = w1.SpeaksDutch && w2.SpeaksDutch;
                var plShared = w1.SpeaksPolish && w2.SpeaksPolish;
                (nlShared || plShared).Should().BeTrue(
                    $"Workers {w1.Name} and {w2.Name} on Warehousing share no language");
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllStrategies))]
    public void Solve_MinimumSkillLevelRequirements(OptimizationStrategy strategy)
    {
        var result = _solver.Solve(_workers, _tasks, strategy);

        foreach (var task in _tasks)
        {
            if (task.MinWorkersLevel1 > 0)
            {
                var l1Count = result.Assignments.Count(a =>
                    a.TaskName == task.Name && a.SkillLevel == SkillLevel.Expert);
                l1Count.Should().BeGreaterThanOrEqualTo(task.MinWorkersLevel1,
                    $"{task.Name}: needs {task.MinWorkersLevel1} level-1 workers, got {l1Count}");
            }
        }
    }
}
