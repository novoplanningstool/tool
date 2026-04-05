using Novo.Planning.Domain.Models;

namespace Novo.Planning.Web.Features.Planning.Services;

public interface IExcelExportService
{
    Task<byte[]> ExportAsync(PlanningModel planning);
}
