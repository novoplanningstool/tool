using Novo.Planning.Domain.Models;

namespace Novo.Planning.Domain.Interfaces;

public interface IPersonRepository
{
    Task<IReadOnlyList<Person>> GetAllAsync();
    Task<Person?> GetByIdAsync(string id);
    Task UpsertAsync(Person person);
    Task DeleteAsync(string id);
}
