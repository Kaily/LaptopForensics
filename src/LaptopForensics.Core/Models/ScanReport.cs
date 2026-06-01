namespace LaptopForensics.Core.Models;

public class ScanReport
{
    public string ScanId { get; } = Guid.NewGuid().ToString();
    public DateTime ScanTimestamp { get; } = DateTime.Now;
    public string Hostname { get; set; } = Environment.MachineName;
    public string OsVersion { get; set; } = string.Empty;
    public ScanMode Mode { get; set; }
    public Dictionary<string, ModuleResult> Results { get; } = new();
    public double OverallScore => Helpers.ScoreCalculator.Calculate(Results);
    public string Grade => Helpers.ScoreCalculator.GetGrade(OverallScore);
    public long TotalDurationMs { get; set; }
    public List<Finding> AllFindings =>
        Results.Values.SelectMany(r => r.Findings).OrderByDescending(f => f.Level).ToList();
}
