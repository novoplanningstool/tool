using Novo.Planning.Domain.Models;

namespace Novo.Planning.Web.Features.Planning.Services;

public interface IComplianceValidator
{
    List<ComplianceViolation> Validate(PlanningModel planning, List<Person> persons, List<TaskDefinition> tasks);
}
