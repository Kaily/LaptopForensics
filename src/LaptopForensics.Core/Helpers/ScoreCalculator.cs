namespace LaptopForensics.Core.Helpers;

public static class ScoreCalculator
{
    public static double Calculate(Dictionary<string, ModuleResult> results)
    {
        if (results.Count == 0)
        {
            return 0;
        }

        var weights = new Dictionary<string, double>
        {
            { "HardwareScanner", 0.15 },
            { "SecurityScanner", 0.25 },
            { "UserScanner", 0.10 },
            { "NetworkScanner", 0.15 },
            { "SoftwareScanner", 0.10 },
            { "LicenseAuditor", 0.10 },
            { "StartupScanner", 0.05 },
            { "BrowserScanner", 0.05 },
            { "PerformanceScanner", 0.03 },
            { "ThreatDetector", 0.02 }
        };

        var present = results.Keys.Where(weights.ContainsKey).ToList();
        if (present.Count == 0)
        {
            return Math.Round(results.Values.Average(r => r.Score), 2);
        }

        var totalWeight = present.Sum(k => weights[k]);
        var weighted = present.Sum(k => (weights[k] / totalWeight) * results[k].Score);
        return Math.Round(weighted, 2);
    }

    public static string GetGrade(double score) => score switch
    {
        >= 90 => "EXCELLENT",
        >= 75 => "GOOD",
        >= 60 => "FAIR",
        _ => "POOR"
    };

    public static string GetGradeColor(string grade) => grade.ToUpperInvariant() switch
    {
        "EXCELLENT" => "green",
        "GOOD" => "blue",
        "FAIR" => "yellow",
        "POOR" => "red",
        _ => "grey"
    };
}
