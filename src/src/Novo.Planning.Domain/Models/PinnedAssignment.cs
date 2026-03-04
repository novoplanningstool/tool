namespace Novo.Planning.Domain.Models;

public class PinnedAssignment
{
    public string TaskId { get; set; } = string.Empty;
    public List<string> WorkerIds { get; set; } = [];
}
