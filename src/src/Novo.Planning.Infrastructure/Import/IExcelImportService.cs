namespace Novo.Planning.Infrastructure.Import;

public interface IExcelImportService
{
    Task ImportAsync(string filePath);
}
