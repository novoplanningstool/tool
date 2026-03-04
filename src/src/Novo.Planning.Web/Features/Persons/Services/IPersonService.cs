using Novo.Planning.Domain.Models;

namespace Novo.Planning.Web.Features.Persons.Services;

public interface IPersonService
{
    Task<IReadOnlyList<Person>> GetAllAsync();
    Task<Person?> GetByIdAsync(string id);
    Task<(bool Success, string? Error)> SaveAsync(Person person);
    Task DeleteAsync(string id);
}
