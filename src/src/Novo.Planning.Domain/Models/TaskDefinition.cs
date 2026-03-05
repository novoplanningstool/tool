namespace Novo.Planning.Domain.Models;

public class TaskDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int HeadcountRequired { get; set; } = 1;
    public BoardPosition BoardPosition { get; set; } = BoardPosition.Left;
    public int MinWorkersLevel1 { get; set; }
    public int MinWorkersLevel2 { get; set; }
    public int MinWorkersLevel3 { get; set; }
    public SkillLevel RestLevel { get; set; } = SkillLevel.Beginner;
    public bool RequiresLanguageCollaboration { get; set; }
    public int SortOrder { get; set; }
}
