namespace Novo.Planning.Domain.Models;

public class CustomTask
{
    public string TaskName { get; set; } = string.Empty;
    public int HeadcountRequired { get; set; } = 2;
    public List<string> AssignedWorkers { get; set; } = [];
}
