namespace Novo.Planning.Domain.Models;

public enum ViolationSeverity
{
    Warning,
    Error
}

public class ComplianceViolation
{
    public ViolationSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? TaskName { get; set; }
    public string? WorkerName { get; set; }
}
