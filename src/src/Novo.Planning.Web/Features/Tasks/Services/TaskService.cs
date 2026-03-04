using Novo.Planning.Domain.Interfaces;
using Novo.Planning.Domain.Models;

namespace Novo.Planning.Web.Features.Tasks.Services;

public class TaskService : ITaskService
{
    private readonly ITaskDefinitionRepository _repository;

    public TaskService(ITaskDefinitionRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<TaskDefinition>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task<TaskDefinition?> GetByIdAsync(string id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<(bool Success, string? Error)> SaveAsync(TaskDefinition task)
    {
        if (string.IsNullOrWhiteSpace(task.Name))
        {
            return (false, "Naam is verplicht.");
        }

        if (task.HeadcountRequired <= 0)
        {
            return (false, "Aantal moet groter zijn dan 0.");
        }

        // Check for unique name
        var allTasks = await _repository.GetAllAsync();
        var duplicate = allTasks.FirstOrDefault(t =>
            t.Name.Equals(task.Name, StringComparison.OrdinalIgnoreCase) && t.Id != task.Id);

        if (duplicate is not null)
        {
            return (false, $"Er bestaat al een taak met de naam '{task.Name}'.");
        }

        await _repository.UpsertAsync(task);
        return (true, null);
    }

    public async Task DeleteAsync(string id)
    {
        await _repository.DeleteAsync(id);
    }
}
