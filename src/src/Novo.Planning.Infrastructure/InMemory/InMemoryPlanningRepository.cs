using System.Collections.Concurrent;
using Novo.Planning.Domain.Interfaces;
using Novo.Planning.Domain.Models;

namespace Novo.Planning.Infrastructure.InMemory;

public class InMemoryPlanningRepository : IPlanningRepository
{
    private readonly ConcurrentDictionary<string, PlanningModel> _store = new();

    public Task<IReadOnlyList<PlanningModel>> GetAllAsync()
    {
        IReadOnlyList<PlanningModel> result = _store.Values.OrderByDescending(p => p.CreatedAt).ToList();
        return Task.FromResult(result);
    }

    public Task<PlanningModel?> GetByIdAsync(string id)
    {
        _store.TryGetValue(id, out var planning);
        return Task.FromResult(planning);
    }

    public Task UpsertAsync(PlanningModel planning)
    {
        _store[planning.Id] = planning;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id)
    {
        _store.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PlanningModel>> GetTemplatesAsync()
    {
        IReadOnlyList<PlanningModel> result = _store.Values
            .Where(p => p.IsTemplate)
            .OrderBy(p => p.TemplateName)
            .ToList();
        return Task.FromResult(result);
    }
}
