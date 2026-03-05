namespace Novo.Planning.Domain.Models;

public enum SolverStatus
{
    Optimal,
    Relaxed,
    Infeasible,
    Other
}

public class SolverResult
{
    public SolverStatus Status { get; set; }
    public List<PlanningAssignment> Assignments { get; set; } = [];
    public int RelaxationAttempt { get; set; }
    public List<string> Warnings { get; set; } = [];
    public List<string> Level4OnlyWorkers { get; set; } = [];
    public List<string> Level4OnlyTasks { get; set; } = [];
}
