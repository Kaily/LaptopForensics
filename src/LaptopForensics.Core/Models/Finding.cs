namespace LaptopForensics.Core.Models;

public record Finding
{
    public required Severity Level { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Recommendation { get; init; }
}

public enum Severity
{
    Info,
    Warning,
    Critical
}
