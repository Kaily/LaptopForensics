namespace LaptopForensics.Core.Models;

public class StartupItem
{
    public string Name { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public StartupCategory Category { get; set; }
    public bool IsEnabled { get; set; }
    public double EstimatedDelaySeconds { get; set; }
    public bool IsSuspicious { get; set; }
}

public enum StartupCategory
{
    Essential,
    Useful,
    Optional,
    Bloatware,
    Suspicious
}
