using Novo.Planning.Domain.Interfaces;
using Novo.Planning.Domain.Models;

namespace Novo.Planning.Web.Features.Persons.Services;

public class PersonService : IPersonService
{
    private readonly IPersonRepository _personRepository;

    public PersonService(IPersonRepository personRepository)
    {
        _personRepository = personRepository;
    }

    public async Task<IReadOnlyList<Person>> GetAllAsync()
    {
        return await _personRepository.GetAllAsync();
    }

    public async Task<Person?> GetByIdAsync(string id)
    {
        return await _personRepository.GetByIdAsync(id);
    }

    public async Task<(bool Success, string? Error)> SaveAsync(Person person)
    {
        if (string.IsNullOrWhiteSpace(person.Name))
        {
            return (false, "Naam is verplicht.");
        }

        if (!person.SpeaksDutch && !person.SpeaksPolish)
        {
            return (false, "Een medewerker moet minimaal één taal spreken.");
        }

        var existing = await _personRepository.GetAllAsync();
        var duplicate = existing.FirstOrDefault(p =>
            p.Name.Equals(person.Name, StringComparison.OrdinalIgnoreCase) && p.Id != person.Id);

        if (duplicate is not null)
        {
            return (false, $"Er bestaat al een medewerker met de naam '{person.Name}'.");
        }

        await _personRepository.UpsertAsync(person);
        return (true, null);
    }

    public async Task DeleteAsync(string id)
    {
        await _personRepository.DeleteAsync(id);
    }
}
