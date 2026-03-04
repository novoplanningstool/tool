using Novo.Planning.Domain.Models;

namespace Novo.Planning.Web.Features.Planning.Services;

public interface IPlanningService
{
    Task<SolverResult> GeneratePlanningAsync(PlanningSettings settings);
    Task<SolverResult> ValidateAssignmentsAsync(PlanningSettings settings, List<PlanningAssignment> assignments);
    Task SavePlanningAsync(PlanningModel planning);
    Task<PlanningModel?> LoadPlanningAsync(string id);
    Task<IReadOnlyList<PlanningModel>> GetAllPlanningsAsync();
    Task<IReadOnlyList<PlanningModel>> GetTemplatesAsync();
    Task DeletePlanningAsync(string id);
    Task<byte[]?> ExportToExcelAsync(string planningId);
}
