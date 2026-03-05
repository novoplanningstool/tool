namespace Novo.Planning.Domain.Models;

public class CustomTask
{
    public string TaskName { get; set; } = string.Empty;
    public List<string> AssignedWorkers { get; set; } = [];
}
