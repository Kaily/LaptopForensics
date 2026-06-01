namespace LaptopForensics.Core.Models;

public record ScanHistory
{
    public int Id { get; init; }
    public required string ScanId { get; init; }
    public required DateTime ScanTimestamp { get; init; }
    public required string Hostname { get; init; }
    public string OsVersion { get; init; } = string.Empty;
    public required string ScanMode { get; init; }
    public double OverallScore { get; init; }
    public string Grade { get; init; } = string.Empty;
    public string ReportJson { get; init; } = string.Empty;
    public long TotalDurationMs { get; init; }
    public DateTime CreatedAt { get; init; }
}
