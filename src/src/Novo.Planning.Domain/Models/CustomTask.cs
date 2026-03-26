namespace Novo.Planning.Domain.Models;

public class CustomTask
{
    public string TaskName { get; set; } = string.Empty;
    public int HeadcountRequired { get; set; } = 2;
    public int MinWorkersLevel1 { get; set; }
    public int MinWorkersLevel2 { get; set; }
    public int MinWorkersLevel3 { get; set; }
    public int SortOrder { get; set; }
    public bool DividerAbove { get; set; }
    public List<string> AssignedWorkers { get; set; } = [];

    public TaskDefinition ToTaskDefinition() => new()
    {
        Name = TaskName,
        HeadcountRequired = HeadcountRequired,
        MinWorkersLevel1 = MinWorkersLevel1,
        MinWorkersLevel2 = MinWorkersLevel2,
        MinWorkersLevel3 = MinWorkersLevel3,
        SortOrder = SortOrder,
        DividerAbove = DividerAbove
    };
}
