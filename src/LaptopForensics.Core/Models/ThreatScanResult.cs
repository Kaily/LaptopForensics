namespace LaptopForensics.Core.Models;

public class ThreatScanResult
{
    public List<ThreatFinding> Threats { get; set; } = new();
    public int FilesScanned { get; set; }
    public bool HostsTampered { get; set; }
    public List<string> SuspiciousHosts { get; set; } = new();
    public bool ThreatsFound => Threats.Any(t => t.Severity != Severity.Info);
}

public class ThreatFinding
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public Severity Severity { get; set; }
}
