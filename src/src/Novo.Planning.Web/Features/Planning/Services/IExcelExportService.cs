using Novo.Planning.Domain.Models;

namespace Novo.Planning.Web.Features.Planning.Services;

public interface IExcelExportService
{
    byte[] Export(PlanningModel planning);
}
