using Novo.Planning.Domain.Models;

namespace Novo.Planning.Web.Features.Planning.Services;

public interface IPlanningService
{
    Task SavePlanningAsync(PlanningModel planning);
}
