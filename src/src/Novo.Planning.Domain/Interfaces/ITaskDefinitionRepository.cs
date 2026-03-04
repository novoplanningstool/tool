using Novo.Planning.Domain.Models;

namespace Novo.Planning.Domain.Interfaces;

public interface ITaskDefinitionRepository
{
    Task<IReadOnlyList<TaskDefinition>> GetAllAsync();
    Task<TaskDefinition?> GetByIdAsync(string id);
    Task UpsertAsync(TaskDefinition taskDefinition);
    Task DeleteAsync(string id);
}
