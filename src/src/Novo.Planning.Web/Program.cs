using Novo.Planning.Infrastructure;
using Novo.Planning.Solver;
using Novo.Planning.Web.Components;
using Novo.Planning.Web.Features.Planning.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Infrastructure (in-memory repositories)
builder.Services.AddInMemoryInfrastructure();

// Solver
builder.Services.AddScoped<ISolverService, SolverService>();

// Feature services
builder.Services.AddScoped<IPlanningService, PlanningService>();
builder.Services.AddScoped<IComplianceValidator, ComplianceValidator>();
builder.Services.AddScoped<IExcelExportService, ExcelExportService>();


var app = builder.Build();

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
