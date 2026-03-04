namespace Novo.Planning.Domain.Models;

public class PlanningSettings
{
    public DateOnly Date { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public OptimizationStrategy Strategy { get; set; }
    public List<string> PresentWorkerIds { get; set; } = [];
    public List<string> ActiveTaskIds { get; set; } = [];
    public Dictionary<string, int> TaskHeadcountOverrides { get; set; } = [];
    public List<CustomTask> CustomTasks { get; set; } = [];
    public List<PinnedAssignment> PinnedAssignments { get; set; } = [];
    public int TempWorkerCount { get; set; }
}
