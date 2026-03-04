using Novo.Planning.Domain.Models;

namespace Novo.Planning.Web.Features.Tasks.Services;

public interface ITaskService
{
    Task<IReadOnlyList<TaskDefinition>> GetAllAsync();
    Task<TaskDefinition?> GetByIdAsync(string id);
    Task<(bool Success, string? Error)> SaveAsync(TaskDefinition task);
    Task DeleteAsync(string id);
}
