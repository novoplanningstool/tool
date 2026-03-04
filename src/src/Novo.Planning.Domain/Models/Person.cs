namespace Novo.Planning.Domain.Models;

public class Person
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public bool SpeaksDutch { get; set; }
    public bool SpeaksPolish { get; set; }
    public HashSet<DayOfWeek> DefaultDaysOff { get; set; } = [];
    public Dictionary<string, SkillLevel> Skills { get; set; } = [];
}
