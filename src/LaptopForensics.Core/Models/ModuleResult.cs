namespace LaptopForensics.Core.Models;

public record ModuleResult
{
    public required string ModuleName { get; init; }
    public required bool Success { get; init; }
    public required double Score { get; init; }
    public required string Grade { get; init; }
    public required List<Finding> Findings { get; init; }
    public required object Data { get; init; }
    public required long DurationMs { get; init; }
    public string? ErrorMessage { get; init; }
}
