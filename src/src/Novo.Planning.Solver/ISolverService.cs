using Novo.Planning.Domain.Models;

namespace Novo.Planning.Solver;

public interface ISolverService
{
    SolverResult Solve(List<Person> presentWorkers, List<TaskDefinition> activeTasks, OptimizationStrategy strategy);
    SolverResult ValidateAssignments(List<Person> presentWorkers, List<TaskDefinition> activeTasks, List<PlanningAssignment> assignments);
}
