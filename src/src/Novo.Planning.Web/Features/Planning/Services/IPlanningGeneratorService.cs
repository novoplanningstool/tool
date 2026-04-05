using Novo.Planning.Domain.Models;

namespace Novo.Planning.Web.Features.Planning.Services;

public interface IPlanningGeneratorService
{
    List<PlanningAssignment> Generate(List<Person> availableWorkers, List<TaskDefinition> selectedTasks);
}
