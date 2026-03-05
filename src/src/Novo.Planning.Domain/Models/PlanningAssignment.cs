namespace Novo.Planning.Domain.Models;

public class PlanningAssignment
{
    public string TaskName { get; set; } = string.Empty;
    public string WorkerName { get; set; } = string.Empty;
    public SkillLevel SkillLevel { get; set; }
}
