namespace Novo.Planning.Domain.Models;

public class CustomTask
{
    public string TaskName { get; set; } = string.Empty;
    public int HeadcountRequired { get; set; } = 2;
    public int MinWorkersLevel1 { get; set; }
    public int MinWorkersLevel2 { get; set; }
    public int MinWorkersLevel3 { get; set; }
    public List<string> AssignedWorkers { get; set; } = [];
}
