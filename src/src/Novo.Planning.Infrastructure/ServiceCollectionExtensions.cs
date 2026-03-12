using Microsoft.Extensions.DependencyInjection;
using Novo.Planning.Domain.Interfaces;
using Novo.Planning.Infrastructure.Import;
using Novo.Planning.Infrastructure.InMemory;

namespace Novo.Planning.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInMemoryInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IPersonRepository, InMemoryPersonRepository>();
        services.AddScoped<ITaskDefinitionRepository, InMemoryTaskDefinitionRepository>();
        services.AddScoped<IPlanningRepository, InMemoryPlanningRepository>();
        services.AddScoped<IExcelImportService, ExcelImportService>();
        return services;
    }
}
