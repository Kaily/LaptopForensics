namespace LaptopForensics.Core.Models;

public class SecurityStatus
{
    public AntivirusStatus Antivirus { get; set; } = new();
    public FirewallStatus Firewall { get; set; } = new();
    public DefenderStatus Defender { get; set; } = new();
    public BitLockerStatus BitLocker { get; set; } = new();
    public UacStatus Uac { get; set; } = new();
    public UpdateStatus WindowsUpdates { get; set; } = new();
    public SecureBootStatus SecureBoot { get; set; } = new();
    public List<string> RecentErrors { get; set; } = new();
}

public class AntivirusStatus
{
    public string ProductName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool IsUpToDate { get; set; }
    public DateTime? LastUpdateDate { get; set; }
}

public class DefenderStatus
{
    public bool RealTimeEnabled { get; set; }
    public bool AntivirusEnabled { get; set; }
    public DateTime? LastScanDate { get; set; }
    public string SignatureVersion { get; set; } = string.Empty;
    public DateTime? SignatureDate { get; set; }
}

public class FirewallStatus
{
    public bool DomainEnabled { get; set; }
    public bool PrivateEnabled { get; set; }
    public bool PublicEnabled { get; set; }
    public bool AllEnabled => DomainEnabled && PrivateEnabled && PublicEnabled;
}

public class BitLockerStatus
{
    public List<DriveEncryptionStatus> Drives { get; set; } = new();
    public bool AllDrivesEncrypted => Drives.All(d => d.IsEncrypted);
}

public class DriveEncryptionStatus
{
    public string DriveLetter { get; set; } = string.Empty;
    public bool IsEncrypted { get; set; }
    public string Method { get; set; } = string.Empty;
}

public class UacStatus
{
    public bool IsEnabled { get; set; }
    public int Level { get; set; }
}

public class UpdateStatus
{
    public DateTime? LastUpdateDate { get; set; }
    public int PendingUpdates { get; set; }
    public List<string> RecentHotfixes { get; set; } = new();
}

public class SecureBootStatus
{
    public bool? IsEnabled { get; set; }
    public bool IsBiosMode { get; set; }
}
