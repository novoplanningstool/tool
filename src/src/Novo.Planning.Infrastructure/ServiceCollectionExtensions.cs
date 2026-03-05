using Microsoft.Extensions.DependencyInjection;
using Novo.Planning.Domain.Interfaces;
using Novo.Planning.Infrastructure.Import;
using Novo.Planning.Infrastructure.InMemory;

namespace Novo.Planning.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInMemoryInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IPersonRepository, InMemoryPersonRepository>();
        services.AddSingleton<ITaskDefinitionRepository, InMemoryTaskDefinitionRepository>();
        services.AddSingleton<IPlanningRepository, InMemoryPlanningRepository>();
        services.AddScoped<IExcelImportService, ExcelImportService>();
        return services;
    }
}
