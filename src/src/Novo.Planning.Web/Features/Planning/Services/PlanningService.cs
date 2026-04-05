using Novo.Planning.Domain.Interfaces;
using Novo.Planning.Domain.Models;

namespace Novo.Planning.Web.Features.Planning.Services;

public class PlanningService : IPlanningService
{
    private readonly IPlanningRepository _planningRepository;

    public PlanningService(IPlanningRepository planningRepository)
    {
        _planningRepository = planningRepository;
    }

    public async Task SavePlanningAsync(PlanningModel planning)
    {
        await _planningRepository.UpsertAsync(planning);
    }
}
