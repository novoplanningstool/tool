namespace Novo.Planning.Domain.Models;

public class PlanningModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateOnly Date { get; set; }
    public string DayName { get; set; } = string.Empty;
    public OptimizationStrategy Strategy { get; set; }
    public List<PlanningAssignment> Assignments { get; set; } = [];
    public List<CustomTask> CustomTasks { get; set; } = [];
    public List<PinnedAssignment> PinnedAssignments { get; set; } = [];
    public List<string> AbsentWorkers { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public bool IsTemplate { get; set; }
    public string? TemplateName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
