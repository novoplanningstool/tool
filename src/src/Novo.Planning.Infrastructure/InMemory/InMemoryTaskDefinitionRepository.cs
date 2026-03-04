using System.Collections.Concurrent;
using Novo.Planning.Domain.Interfaces;
using Novo.Planning.Domain.Models;

namespace Novo.Planning.Infrastructure.InMemory;

public class InMemoryTaskDefinitionRepository : ITaskDefinitionRepository
{
    private readonly ConcurrentDictionary<string, TaskDefinition> _store = new();

    public Task<IReadOnlyList<TaskDefinition>> GetAllAsync()
    {
        IReadOnlyList<TaskDefinition> result = _store.Values.OrderBy(t => t.SortOrder).ThenBy(t => t.Name).ToList();
        return Task.FromResult(result);
    }

    public Task<TaskDefinition?> GetByIdAsync(string id)
    {
        _store.TryGetValue(id, out var task);
        return Task.FromResult(task);
    }

    public Task UpsertAsync(TaskDefinition taskDefinition)
    {
        _store[taskDefinition.Id] = taskDefinition;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id)
    {
        _store.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
