using FluentAssertions;
using Novo.Planning.Domain.Models;
using Novo.Planning.Infrastructure.InMemory;

namespace Novo.Planning.Infrastructure.Tests;

public class InMemoryPersonRepositoryTests
{
    private readonly InMemoryPersonRepository _repo = new();

    [Fact]
    public async Task GetAllAsync_Empty_ReturnsEmptyList()
    {
        var result = await _repo.GetAllAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UpsertAsync_Then_GetByIdAsync_ReturnsPerson()
    {
        var person = new Person { Id = "p1", Name = "Alice" };
        await _repo.UpsertAsync(person);

        var result = await _repo.GetByIdAsync("p1");
        result.Should().NotBeNull();
        result!.Name.Should().Be("Alice");
    }

    [Fact]
    public async Task UpsertAsync_Update_OverwritesExisting()
    {
        var person = new Person { Id = "p1", Name = "Alice" };
        await _repo.UpsertAsync(person);

        person.Name = "Alice Updated";
        await _repo.UpsertAsync(person);

        var result = await _repo.GetByIdAsync("p1");
        result!.Name.Should().Be("Alice Updated");
    }

    [Fact]
    public async Task DeleteAsync_RemovesPerson()
    {
        var person = new Person { Id = "p1", Name = "Alice" };
        await _repo.UpsertAsync(person);
        await _repo.DeleteAsync("p1");

        var result = await _repo.GetByIdAsync("p1");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAll_OrderedByName()
    {
        await _repo.UpsertAsync(new Person { Id = "p2", Name = "Zara" });
        await _repo.UpsertAsync(new Person { Id = "p1", Name = "Alice" });

        var result = await _repo.GetAllAsync();
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Alice");
        result[1].Name.Should().Be("Zara");
    }
}

public class InMemoryTaskDefinitionRepositoryTests
{
    private readonly InMemoryTaskDefinitionRepository _repo = new();

    [Fact]
    public async Task UpsertAsync_Then_GetByIdAsync_ReturnsTask()
    {
        var task = new TaskDefinition { Id = "t1", Name = "Ompakken" };
        await _repo.UpsertAsync(task);

        var result = await _repo.GetByIdAsync("t1");
        result.Should().NotBeNull();
        result!.Name.Should().Be("Ompakken");
    }

    [Fact]
    public async Task DeleteAsync_RemovesTask()
    {
        var task = new TaskDefinition { Id = "t1", Name = "Ompakken" };
        await _repo.UpsertAsync(task);
        await _repo.DeleteAsync("t1");

        var result = await _repo.GetByIdAsync("t1");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_OrdersBySortOrderThenName()
    {
        await _repo.UpsertAsync(new TaskDefinition { Id = "t2", Name = "B Task", SortOrder = 1 });
        await _repo.UpsertAsync(new TaskDefinition { Id = "t1", Name = "A Task", SortOrder = 0 });

        var result = await _repo.GetAllAsync();
        result[0].Name.Should().Be("A Task");
        result[1].Name.Should().Be("B Task");
    }
}

public class InMemoryPlanningRepositoryTests
{
    private readonly InMemoryPlanningRepository _repo = new();

    [Fact]
    public async Task UpsertAsync_Then_GetByIdAsync_ReturnsPlanning()
    {
        var planning = new PlanningModel { Id = "pl1", DayName = "maandag" };
        await _repo.UpsertAsync(planning);

        var result = await _repo.GetByIdAsync("pl1");
        result.Should().NotBeNull();
        result!.DayName.Should().Be("maandag");
    }

    [Fact]
    public async Task GetTemplatesAsync_ReturnsOnlyTemplates()
    {
        await _repo.UpsertAsync(new PlanningModel { Id = "pl1", IsTemplate = false });
        await _repo.UpsertAsync(new PlanningModel { Id = "pl2", IsTemplate = true, TemplateName = "Template A" });

        var templates = await _repo.GetTemplatesAsync();
        templates.Should().HaveCount(1);
        templates[0].TemplateName.Should().Be("Template A");
    }

    [Fact]
    public async Task DeleteAsync_RemovesPlanning()
    {
        await _repo.UpsertAsync(new PlanningModel { Id = "pl1" });
        await _repo.DeleteAsync("pl1");

        var result = await _repo.GetByIdAsync("pl1");
        result.Should().BeNull();
    }
}
