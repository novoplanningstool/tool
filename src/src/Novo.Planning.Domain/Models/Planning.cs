namespace Novo.Planning.Domain.Models;

public class PlanningModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DayName { get; set; } = string.Empty;
    public List<PlanningAssignment> Assignments { get; set; } = [];
    public List<CustomTask> CustomTasks { get; set; } = [];
    public List<PinnedAssignment> PinnedAssignments { get; set; } = [];
    public List<string> AbsentWorkers { get; set; } = [];
    public bool IsTemplate { get; set; }
    public string? TemplateName { get; set; }
    public int TempWorkerCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
