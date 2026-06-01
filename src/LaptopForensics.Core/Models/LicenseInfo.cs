namespace LaptopForensics.Core.Models;

public class LicenseInfo
{
    public WindowsLicense Windows { get; set; } = new();
    public OfficeLicense? Office { get; set; }
    public List<SoftwareLicense> Others { get; set; } = new();

    public int LicensedCount => Others.Count(l => l.Status == LicenseStatus.Licensed);
    public int FreeCount => Others.Count(l => l.Status == LicenseStatus.Free);
    public int TrialCount => Others.Count(l => l.Status == LicenseStatus.Trial);
    public int ExpiredCount => Others.Count(l => l.Status == LicenseStatus.Expired);
    public int UnknownCount => Others.Count(l => l.Status == LicenseStatus.Unknown);
    public double CompliancePercent => Others.Count > 0 ? (LicensedCount + FreeCount) / (double)Others.Count * 100 : 100;
}

public class WindowsLicense
{
    public string Edition { get; set; } = string.Empty;
    public string ActivationStatus { get; set; } = string.Empty;
    public string? ProductKey { get; set; }
    public string LicenseType { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
}

public class OfficeLicense
{
    public string Version { get; set; } = string.Empty;
    public string ActivationStatus { get; set; } = string.Empty;
    public string LicensedTo { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
}

public class SoftwareLicense
{
    public string AppName { get; set; } = string.Empty;
    public LicenseStatus Status { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int? DaysUntilExpiry { get; set; }
    public string Note { get; set; } = string.Empty;
}
