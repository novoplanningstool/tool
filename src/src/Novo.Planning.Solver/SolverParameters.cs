using Novo.Planning.Domain.Models;

namespace Novo.Planning.Solver;

/// <summary>
/// Bundles all pre-computed arrays and indices needed by the MIP model.
/// Port of Python SolverParams dataclass.
/// </summary>
public class SolverParameters
{
    /// <summary>Skill levels to consider (1, 2, 3).</summary>
    public required int[] Levels { get; init; }

    /// <summary>Task indices (0..N-1).</summary>
    public required int[] TaskIndices { get; init; }

    /// <summary>Worker indices (0..M-1).</summary>
    public required int[] WorkerIndices { get; init; }

    /// <summary>Task names indexed by task index.</summary>
    public required string[] TaskNames { get; init; }

    /// <summary>Worker names indexed by worker index.</summary>
    public required string[] WorkerNames { get; init; }

    /// <summary>skill_matrix[workerIdx, taskIdx] = skill level (1-4).</summary>
    public required int[,] SkillMatrix { get; init; }

    /// <summary>Tuples of (level, worker, task) for decision variable indexing.</summary>
    public required (int Level, int Worker, int Task)[] SkillIndexTuples { get; init; }

    /// <summary>1 if worker eligible at level for task, 0 otherwise.</summary>
    public required int[] SkillEligible { get; init; }

    /// <summary>Tuples of (level, task) for headcount indexing.</summary>
    public required (int Level, int Task)[] TeamsizeIndexTuples { get; init; }

    /// <summary>Minimum workers per level per task.</summary>
    public required int[] MinWorkersPerLevel { get; init; }

    /// <summary>Tuples of (worker1, worker2) for language indexing.</summary>
    public required (int Worker1, int Worker2)[] LanguageIndexTuples { get; init; }

    /// <summary>1 if workers are language-incompatible, 0 if compatible.</summary>
    public required int[] LanguageIncompatible { get; init; }

    /// <summary>Required headcount per task.</summary>
    public required int[] TaskHeadcounts { get; init; }

    /// <summary>Whether task requires language collaboration (Samenwerken).</summary>
    public required bool[] TaskRequiresCollaboration { get; init; }

    /// <summary>Min level-1 workers per task.</summary>
    public required int[] MinLevel1PerTask { get; init; }

    /// <summary>Max level-3 workers per task (from Aantal_min_niveau_3).</summary>
    public required int[] MaxLevel3PerTask { get; init; }

    /// <summary>Dutch speaker flags indexed by worker.</summary>
    public required bool[] WorkerSpeaksDutch { get; init; }

    /// <summary>Polish speaker flags indexed by worker.</summary>
    public required bool[] WorkerSpeaksPolish { get; init; }
}
