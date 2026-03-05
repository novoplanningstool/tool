using FluentAssertions;
using Novo.Planning.Domain.Models;

namespace Novo.Planning.Solver.Tests;

/// <summary>
/// Port of TestBuildSolverParameters from test_solver.py.
/// </summary>
public class SolverParameterBuilderTests
{
    private readonly List<Person> _workers = SolverTestFixtures.CreateSmallWorkers();
    private readonly List<TaskDefinition> _tasks = SolverTestFixtures.CreateSmallTasks();

    [Fact]
    public void Build_ReturnsValidParameters()
    {
        var result = SolverParameterBuilder.Build(_workers, _tasks);

        result.Should().NotBeNull();
        result.Levels.Should().BeEquivalentTo([1, 2, 3]);
    }

    [Fact]
    public void Build_SkillIndexTuplesLength_CorrectForSmallFixture()
    {
        var parameters = SolverParameterBuilder.Build(_workers, _tasks);

        // 3 levels * 3 workers * 2 tasks = 18
        parameters.SkillIndexTuples.Should().HaveCount(3 * 3 * 2);
    }

    [Fact]
    public void Build_ExpertIsEligibleAtAllLevels()
    {
        // Alice has level 1 on Ompakken -> eligible at levels 1, 2, 3
        var parameters = SolverParameterBuilder.Build(_workers, _tasks);
        int aliceIdx = 0, ompakkenIdx = 0;

        foreach (var level in new[] { 1, 2, 3 })
        {
            var idx = FindSkillIndex(parameters, level, aliceIdx, ompakkenIdx);
            parameters.SkillEligible[idx].Should().Be(1,
                $"Alice (expert) should be eligible at level {level}");
        }
    }

    [Fact]
    public void Build_BeginnerOnlyEligibleAtLevel3()
    {
        // Charlie has level 3 on Ompakken -> eligible only at level 3
        var parameters = SolverParameterBuilder.Build(_workers, _tasks);
        int charlieIdx = 2, ompakkenIdx = 0;

        foreach (var level in new[] { 1, 2 })
        {
            var idx = FindSkillIndex(parameters, level, charlieIdx, ompakkenIdx);
            parameters.SkillEligible[idx].Should().Be(0,
                $"Charlie (beginner) should NOT be eligible at level {level}");
        }

        var l3Idx = FindSkillIndex(parameters, 3, charlieIdx, ompakkenIdx);
        parameters.SkillEligible[l3Idx].Should().Be(1,
            "Charlie (beginner) should be eligible at level 3");
    }

    [Fact]
    public void Build_LanguageCompatible_SharedDutch()
    {
        // Alice(NL) + Bob(NL) share Dutch -> compatible (0)
        var parameters = SolverParameterBuilder.Build(_workers, _tasks);
        int aliceIdx = 0, bobIdx = 1;

        var langIdx = FindLanguageIndex(parameters, aliceIdx, bobIdx);
        parameters.LanguageIncompatible[langIdx].Should().Be(0,
            "Alice and Bob both speak Dutch, should be compatible");
    }

    [Fact]
    public void Build_LanguageIncompatible_NoSharedLanguage()
    {
        // Alice(NL) + Charlie(PL) share no language -> incompatible (1)
        var parameters = SolverParameterBuilder.Build(_workers, _tasks);
        int aliceIdx = 0, charlieIdx = 2;

        var langIdx = FindLanguageIndex(parameters, aliceIdx, charlieIdx);
        parameters.LanguageIncompatible[langIdx].Should().Be(1,
            "Alice and Charlie share no language, should be incompatible");
    }

    private static int FindSkillIndex(SolverParameters parameters, int level, int worker, int task)
    {
        for (int i = 0; i < parameters.SkillIndexTuples.Length; i++)
        {
            var (l, w, t) = parameters.SkillIndexTuples[i];
            if (l == level && w == worker && t == task) return i;
        }
        throw new InvalidOperationException($"Skill index not found for ({level}, {worker}, {task})");
    }

    private static int FindLanguageIndex(SolverParameters parameters, int worker1, int worker2)
    {
        for (int i = 0; i < parameters.LanguageIndexTuples.Length; i++)
        {
            var (w1, w2) = parameters.LanguageIndexTuples[i];
            if (w1 == worker1 && w2 == worker2) return i;
        }
        throw new InvalidOperationException($"Language index not found for ({worker1}, {worker2})");
    }
}
