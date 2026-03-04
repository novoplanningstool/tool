using Novo.Planning.Domain.Models;

namespace Novo.Planning.Domain.Interfaces;

public interface IPlanningRepository
{
    Task<IReadOnlyList<PlanningModel>> GetAllAsync();
    Task<PlanningModel?> GetByIdAsync(string id);
    Task UpsertAsync(PlanningModel planning);
    Task DeleteAsync(string id);
    Task<IReadOnlyList<PlanningModel>> GetTemplatesAsync();
}
