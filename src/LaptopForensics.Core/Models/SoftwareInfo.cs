namespace LaptopForensics.Core.Models;

public class SoftwareInfo
{
    public List<InstalledApp> Applications { get; set; } = new();
    public int TotalCount => Applications.Count;
    public long TotalSizeBytes => Applications.Sum(a => a.EstimatedSizeBytes);
    public int SystemApps => Applications.Count(a => a.Category == AppCategory.System);
    public int DevApps => Applications.Count(a => a.Category == AppCategory.Development);
    public int ProductivityApps => Applications.Count(a => a.Category == AppCategory.Productivity);
    public int Bloatware => Applications.Count(a => a.Category == AppCategory.Bloatware);
    public int TrialApps => Applications.Count(a => a.LicenseStatus == LicenseStatus.Trial);
    public int ExpiredApps => Applications.Count(a => a.LicenseStatus == LicenseStatus.Expired);
}

public class InstalledApp
{
    public string DisplayName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public DateTime? InstallDate { get; set; }
    public string InstallLocation { get; set; } = string.Empty;
    public long EstimatedSizeBytes { get; set; }
    public string UninstallString { get; set; } = string.Empty;
    public AppCategory Category { get; set; }
    public LicenseStatus LicenseStatus { get; set; }
    public string RegistrySource { get; set; } = string.Empty;
    public bool Is64Bit { get; set; }
}

public enum AppCategory
{
    System,
    Development,
    Productivity,
    Utilities,
    Security,
    Media,
    Games,
    Bloatware,
    Unknown
}

public enum LicenseStatus
{
    Licensed,
    Free,
    Trial,
    Expired,
    Cracked,
    Unknown
}
