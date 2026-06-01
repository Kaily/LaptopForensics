namespace LaptopForensics.Core.Models;

public class BrowserExtension
{
    public string BrowserName { get; set; } = string.Empty;
    public string ExtensionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public List<string> Permissions { get; set; } = new();
    public RiskLevel Risk { get; set; }
}

public enum RiskLevel
{
    Low,
    Medium,
    High
}
