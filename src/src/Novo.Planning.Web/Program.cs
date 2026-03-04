using Novo.Planning.Infrastructure;
using Novo.Planning.Infrastructure.Import;
using Novo.Planning.Solver;
using Novo.Planning.Web.Components;
using Novo.Planning.Web.Features.Planning.Services;
using Novo.Planning.Web.Features.Persons.Services;
using Novo.Planning.Web.Features.Tasks.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Infrastructure (in-memory repositories)
builder.Services.AddInMemoryInfrastructure();

// Solver
builder.Services.AddScoped<ISolverService, SolverService>();

// Feature services
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IPersonService, PersonService>();
builder.Services.AddScoped<IPlanningService, PlanningService>();
builder.Services.AddScoped<IComplianceValidator, ComplianceValidator>();
builder.Services.AddScoped<IExcelExportService, ExcelExportService>();


var app = builder.Build();

// Seed from Excel if configured
var seedFilePath = builder.Configuration["SeedData:FilePath"];
if (!string.IsNullOrEmpty(seedFilePath))
{
    using var scope = app.Services.CreateScope();
    var importService = scope.ServiceProvider.GetRequiredService<IExcelImportService>();
    var fullPath = Path.IsPathRooted(seedFilePath)
        ? seedFilePath
        : Path.Combine(app.Environment.ContentRootPath, seedFilePath);

    if (File.Exists(fullPath))
    {
        await importService.ImportAsync(fullPath);
        Console.WriteLine($"Seeded data from {fullPath}");
    }
    else
    {
        Console.WriteLine($"Seed file not found: {fullPath}");
    }

    // Seed "Laden/lossen Zeelandia" task if not already present
    var taskRepo = scope.ServiceProvider.GetRequiredService<Novo.Planning.Domain.Interfaces.ITaskDefinitionRepository>();
    var existingTasks = await taskRepo.GetAllAsync();
    if (!existingTasks.Any(t => t.Name.Equals("Laden/lossen Zeelandia", StringComparison.OrdinalIgnoreCase)))
    {
        var maxSort = existingTasks.Any() ? existingTasks.Max(t => t.SortOrder) + 1 : 0;
        await taskRepo.UpsertAsync(new Novo.Planning.Domain.Models.TaskDefinition
        {
            Name = "Laden/lossen Zeelandia",
            IsActive = false,
            HeadcountRequired = 2,
            BoardPosition = Novo.Planning.Domain.Models.BoardPosition.Left,
            SortOrder = maxSort
        });
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Excel export download endpoint
app.MapGet("/api/planning/{id}/export", async (string id, IPlanningService planningService) =>
{
    var bytes = await planningService.ExportToExcelAsync(id);
    if (bytes == null) return Results.NotFound();
    return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "planning.xlsx");
});

app.Run();
