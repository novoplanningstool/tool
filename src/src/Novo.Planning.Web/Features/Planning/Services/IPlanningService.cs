using Novo.Planning.Domain.Models;

namespace Novo.Planning.Web.Features.Planning.Services;

public interface IPlanningService
{
    Task<SolverResult> GeneratePlanningAsync(PlanningSettings settings);
    Task<SolverResult> ValidateAssignmentsAsync(PlanningSettings settings, List<PlanningAssignment> assignments);
    Task SavePlanningAsync(PlanningModel planning);
    Task<byte[]?> ExportToExcelAsync(string planningId);
}
