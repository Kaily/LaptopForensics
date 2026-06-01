namespace LaptopForensics.Core.Models;

public record Finding
{
    public required Severity Level { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Recommendation { get; init; }
    public ConfidenceLevel Confidence { get; init; } = ConfidenceLevel.Medium;
    public Dictionary<string, string> Evidence { get; init; } = new();
}

public enum Severity
{
    Info,
    Warning,
    Critical
}

public enum ConfidenceLevel
{
    Low,
    Medium,
    High
}
