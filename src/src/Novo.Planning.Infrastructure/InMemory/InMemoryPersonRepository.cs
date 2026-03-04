using System.Collections.Concurrent;
using Novo.Planning.Domain.Interfaces;
using Novo.Planning.Domain.Models;

namespace Novo.Planning.Infrastructure.InMemory;

public class InMemoryPersonRepository : IPersonRepository
{
    private readonly ConcurrentDictionary<string, Person> _store = new();

    public Task<IReadOnlyList<Person>> GetAllAsync()
    {
        IReadOnlyList<Person> result = _store.Values.OrderBy(p => p.Name).ToList();
        return Task.FromResult(result);
    }

    public Task<Person?> GetByIdAsync(string id)
    {
        _store.TryGetValue(id, out var person);
        return Task.FromResult(person);
    }

    public Task UpsertAsync(Person person)
    {
        _store[person.Id] = person;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id)
    {
        _store.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
