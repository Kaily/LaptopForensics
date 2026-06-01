# MODULE_CONTRACTS.md
# Laptop Forensics Scanner — Module Contracts
# Version: 1.0 | Last Updated: 2026-05-31
# Author: Kailash Singh Bhakuni

---

## ⚠️ AGENT INSTRUCTION
> This file defines the EXACT contract for every scanner and exporter.
> When implementing any scanner:
>   1. Return EXACTLY the model defined here
>   2. Use EXACTLY the WMI queries listed
>   3. Follow EXACTLY the scoring rules
>   4. Never add fields not listed here (add to this file first)

---

## CONTRACT FORMAT

Each module contract contains:
- **Dependencies** — what gets injected
- **Output Model** — exact C# class to return as `Data` in `ModuleResult`
- **Data Sources** — WMI / Registry / API calls to make
- **Score Rules** — how to calculate 0-100 score
- **Finding Rules** — what triggers Info / Warning / Critical findings
- **Sample Output** — what console should show

---

## MODULE 1: HardwareScanner

### Dependencies
```csharp
private readonly IWmiService _wmi;
private readonly ILogger<HardwareScanner> _logger;
```

### Output Model
```csharp
public class HardwareInfo
{
    public CpuInfo         Cpu         { get; set; } = new();
    public List<RamSlot>   Ram         { get; set; } = new();
    public List<DiskInfo>  Disks       { get; set; } = new();
    public GpuInfo         Gpu         { get; set; } = new();
    public BatteryInfo     Battery     { get; set; } = new();
    public MotherboardInfo Motherboard { get; set; } = new();

    public double TotalRamGb     => Ram.Sum(r => r.CapacityGb);
    public double FreeRamGb      { get; set; }
    public double RamUsagePercent => TotalRamGb > 0 ? (1 - FreeRamGb / TotalRamGb) * 100 : 0;
}

public class CpuInfo
{
    public string Name            { get; set; } = string.Empty;
    public int    NumberOfCores   { get; set; }
    public int    NumberOfThreads { get; set; }
    public double MaxClockGhz     { get; set; }
    public double CurrentClockGhz { get; set; }
    public int    L2CacheMb       { get; set; }
    public int    L3CacheMb       { get; set; }
    public string Architecture    { get; set; } = string.Empty; // x64/ARM
    public double? TemperatureCelsius { get; set; }           // null if unavailable
}

public class RamSlot
{
    public string  BankLabel      { get; set; } = string.Empty;
    public double  CapacityGb     { get; set; }
    public int     SpeedMhz       { get; set; }
    public string  MemoryType     { get; set; } = string.Empty; // DDR4/DDR5
    public string  Manufacturer   { get; set; } = string.Empty;
    public string  PartNumber     { get; set; } = string.Empty;
}

public class DiskInfo
{
    public string  Model          { get; set; } = string.Empty;
    public string  Interface      { get; set; } = string.Empty; // NVMe/SATA/HDD
    public double  SizeGb         { get; set; }
    public double  FreeGb         { get; set; }
    public int     HealthPercent  { get; set; }                // from SMART
    public int?    TemperatureCelsius { get; set; }
    public long?   PowerOnHours   { get; set; }
    public string  DriveLetter    { get; set; } = string.Empty;
    public string  FileSystem     { get; set; } = string.Empty;
}

public class GpuInfo
{
    public string Name            { get; set; } = string.Empty;
    public double VramGb          { get; set; }
    public string DriverVersion   { get; set; } = string.Empty;
    public string Resolution      { get; set; } = string.Empty;
    public double? TemperatureCelsius { get; set; }
}

public class BatteryInfo
{
    public bool   IsPresent       { get; set; }
    public double DesignCapacityWh { get; set; }
    public double CurrentCapacityWh { get; set; }
    public int    ChargePercent   { get; set; }
    public int    HealthPercent   { get; set; }  // CurrentCapacity/DesignCapacity * 100
    public int?   CycleCount      { get; set; }
    public string Status          { get; set; } = string.Empty; // Charging/Discharging/Full
}

public class MotherboardInfo
{
    public string Manufacturer    { get; set; } = string.Empty;
    public string Product         { get; set; } = string.Empty;
    public string SerialNumber    { get; set; } = string.Empty;
    public string BiosVersion     { get; set; } = string.Empty;
    public string BiosDate        { get; set; } = string.Empty;
}
```

### Data Sources
```
CPU:         WMI Win32_Processor
             Fields: Name, NumberOfCores, NumberOfLogicalProcessors,
                     MaxClockSpeed, CurrentClockSpeed, L2CacheSize, L3CacheSize

RAM:         WMI Win32_PhysicalMemory
             Fields: BankLabel, Capacity, Speed, MemoryType, Manufacturer, PartNumber
             Free RAM: WMI Win32_OperatingSystem.FreePhysicalMemory

Disk:        WMI Win32_DiskDrive (physical drives)
             WMI Win32_LogicalDisk (drive letters + free space)
             SMART: DeviceIoControl IOCTL_STORAGE_QUERY_PROPERTY (best effort)

GPU:         WMI Win32_VideoController
             Fields: Name, AdapterRAM, DriverVersion, CurrentHorizontalResolution,
                     CurrentVerticalResolution

Battery:     WMI Win32_Battery
             Fields: EstimatedChargeRemaining, DesignCapacity, FullChargeCapacity,
                     BatteryStatus, CycleCount (not always available)

Motherboard: WMI Win32_BaseBoard (Manufacturer, Product, SerialNumber)
             WMI Win32_BIOS (Name, Version, ReleaseDate)
```

### Score Rules
```
Start at 100. Deduct:
  -20 if any disk SMART health < 80%
  -15 if CPU temperature > 90°C
  -10 if RAM usage > 90%
  -10 if any disk free space < 5 GB
  -10 if battery health < 70%
  -5  if battery health < 85%
  -5  if disk health < 95%
```

### Finding Rules
```
CRITICAL: Disk SMART health < 70%       → "Disk failure imminent"
CRITICAL: CPU temperature > 95°C        → "CPU overheating"
WARNING:  Disk free space < 5 GB        → "Low disk space"
WARNING:  Battery health < 70%          → "Battery degraded"
WARNING:  RAM usage > 85%               → "High memory usage"
INFO:     Battery cycle count > 500     → "Battery aging"
INFO:     Disk health < 95%             → "Disk showing minor wear"
```

---

## MODULE 2: UserScanner

### Dependencies
```csharp
private readonly IWmiService _wmi;
private readonly IPowerShellService _ps;
private readonly ILogger<UserScanner> _logger;
```

### Output Model
```csharp
public class UserAccountInfo
{
    public List<LocalAccount>  Accounts        { get; set; } = new();
    public List<ActiveSession> ActiveSessions  { get; set; } = new();
    public List<LoginEvent>    LoginHistory    { get; set; } = new();
    public PasswordPolicy      PasswordPolicy  { get; set; } = new();
    public int                 TotalAccounts   => Accounts.Count;
    public int                 ActiveAccounts  => Accounts.Count(a => a.IsEnabled);
    public int                 AdminAccounts   => Accounts.Count(a => a.IsAdmin);
    public int                 InactiveAccounts => Accounts.Count(a =>
                                    a.LastLogin < DateTime.Now.AddDays(-90));
}

public class LocalAccount
{
    public string        Name           { get; set; } = string.Empty;
    public string        FullName       { get; set; } = string.Empty;
    public string        Sid            { get; set; } = string.Empty;
    public bool          IsEnabled      { get; set; }
    public bool          IsAdmin        { get; set; }
    public bool          IsBuiltIn      { get; set; }
    public DateTime?     LastLogin      { get; set; }
    public DateTime?     AccountCreated { get; set; }
    public DateTime?     PasswordLastSet { get; set; }
    public bool          PasswordNeverExpires { get; set; }
    public List<string>  Groups         { get; set; } = new();
    public string        ProfilePath    { get; set; } = string.Empty;
    public long          ProfileSizeBytes { get; set; }
    public int           FailedLoginCount30Days { get; set; }
}

public class ActiveSession
{
    public string    Username      { get; set; } = string.Empty;
    public int       SessionId     { get; set; }
    public string    LogonType     { get; set; } = string.Empty; // Interactive/Remote/etc
    public DateTime  LoginTime     { get; set; }
    public TimeSpan  IdleTime      { get; set; }
    public string?   RemoteIp      { get; set; }
}

public class LoginEvent
{
    public DateTime  Timestamp     { get; set; }
    public string    Username      { get; set; } = string.Empty;
    public bool      Success       { get; set; }
    public string    LogonType     { get; set; } = string.Empty;
    public string?   RemoteIp      { get; set; }
    public int       EventId       { get; set; } // 4624=success, 4625=failure
}

public class PasswordPolicy
{
    public int    MinimumLength      { get; set; }
    public bool   ComplexityRequired { get; set; }
    public int    MaxPasswordAgeDays { get; set; }
    public int    LockoutThreshold   { get; set; }
}
```

### Data Sources
```
Local accounts:  WMI Win32_UserAccount (LocalAccount=True)
Groups:          WMI Win32_GroupUser
Last login:      WMI Win32_NetworkLoginProfile
Active sessions: WMI Win32_LogonSession + Win32_LoggedOnUser
Profile size:    DirectoryInfo on C:\Users\[username] — recursive
Login history:   Windows Event Log, Security log
                 EventID 4624 = successful logon
                 EventID 4625 = failed logon
                 Last 50 events only
Password policy: PowerShell: net accounts (parse output)
```

### Score Rules
```
Start at 100. Deduct:
  -25 if any enabled account with password never set
  -20 if any inactive account (90+ days) still enabled
  -15 if admin account has no password
  -15 if Guest account is enabled
  -10 if 3+ failed logins for any account in 30 days
  -10 if built-in Administrator account is enabled
  -5  if password complexity not required
  -5  if password min length < 8
```

### Finding Rules
```
CRITICAL: Guest account enabled          → "Guest account is a security risk"
CRITICAL: 10+ failed logins same account → "Possible brute force attempt"
WARNING:  Inactive account still enabled → "Inactive account: [name]"
WARNING:  Password never expires         → "Account [name] has no password expiry"
WARNING:  Built-in Admin account enabled → "Built-in Administrator account is active"
INFO:     Admin account count > 2        → "Multiple admin accounts detected"
INFO:     Old profile exists             → "Unused profile: [name], [size]"
```

---

## MODULE 3: NetworkScanner

### Dependencies
```csharp
private readonly IPowerShellService _ps;
private readonly ILogger<NetworkScanner> _logger;
```

### Output Model
```csharp
public class NetworkInfo
{
    public List<NetworkAdapter>     Adapters       { get; set; } = new();
    public List<TcpConnectionInfo>  Connections    { get; set; } = new();
    public List<UdpPortInfo>        ListeningPorts { get; set; } = new();
    public List<SavedWifiNetwork>   SavedNetworks  { get; set; } = new();
    public PingResult               PingTest       { get; set; } = new();
    public int                      FirewallRuleCount { get; set; }
    public List<string>             SuspiciousConnections { get; set; } = new();
}

public class NetworkAdapter
{
    public string        Name            { get; set; } = string.Empty;
    public string        Type            { get; set; } = string.Empty; // Ethernet/WiFi/VPN
    public string        MacAddress      { get; set; } = string.Empty;
    public bool          IsConnected     { get; set; }
    public List<string>  IpAddresses     { get; set; } = new();
    public string        SubnetMask      { get; set; } = string.Empty;
    public string        DefaultGateway  { get; set; } = string.Empty;
    public List<string>  DnsServers      { get; set; } = new();
    public bool          DhcpEnabled     { get; set; }
    public long          SpeedMbps       { get; set; }
    public long          BytesSent       { get; set; }
    public long          BytesReceived   { get; set; }
    public string?       ConnectedSsid   { get; set; }  // WiFi only
}

public class TcpConnectionInfo
{
    public string  LocalAddress    { get; set; } = string.Empty;
    public int     LocalPort       { get; set; }
    public string  RemoteAddress   { get; set; } = string.Empty;
    public int     RemotePort      { get; set; }
    public string  State           { get; set; } = string.Empty;
    public string? ProcessName     { get; set; }
    public int?    Pid             { get; set; }
    public string? RemoteHostname  { get; set; }
    public bool    IsSuspicious    { get; set; }
}

public class UdpPortInfo
{
    public string  LocalAddress    { get; set; } = string.Empty;
    public int     LocalPort       { get; set; }
    public string? ProcessName     { get; set; }
    public int?    Pid             { get; set; }
}

public class SavedWifiNetwork
{
    public string Name             { get; set; } = string.Empty;
    public string Security         { get; set; } = string.Empty; // WPA2/WPA3/Open
}

public class PingResult
{
    public double AverageMs        { get; set; }
    public double MinMs            { get; set; }
    public double MaxMs            { get; set; }
    public int    PacketLoss       { get; set; }  // percentage
}
```

### Data Sources
```
Adapters:      System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
TCP:           IPGlobalProperties.GetActiveTcpConnections()
               Map PID: use GetExtendedTcpTable P/Invoke (MIB_TCPTABLE2)
UDP:           IPGlobalProperties.GetActiveUdpListeners()
Process name:  Process.GetProcessById(pid).ProcessName
Hostname:      Dns.GetHostEntry(ip) with 1000ms timeout, catch all exceptions
WiFi:          PowerShell: netsh wlan show profiles → parse names
               Per network: netsh wlan show profile name="X" key=clear → parse security
Firewall:      PowerShell: netsh advfirewall show allprofiles → parse rule count
Ping:          Ping class, target 8.8.8.8, 5 attempts, average RTT
```

### Score Rules
```
Start at 100. Deduct:
  -20 if any suspicious connections detected
  -15 if firewall is disabled on any profile
  -15 if SMB port 445 listening on all interfaces (0.0.0.0)
  -10 if RDP port 3389 listening on all interfaces
  -10 if Telnet port 23 open
  -10 if FTP port 21 open
  -5  if DNS using ISP/private DNS (not 1.1.1.1 / 8.8.8.8)
  -5  if packet loss > 2%
```

### Finding Rules
```
CRITICAL: Connection to known malicious IP range  → "Suspicious outbound connection"
CRITICAL: Telnet port 23 open externally          → "Insecure protocol enabled"
WARNING:  RDP open on all interfaces              → "Remote Desktop exposed"
WARNING:  SMB open on all interfaces              → "File sharing exposed"
WARNING:  FTP port 21 open                        → "Insecure FTP enabled"
INFO:     10+ active connections                  → "High number of connections: [count]"
INFO:     ISP DNS in use                          → "Consider using 1.1.1.1 or 8.8.8.8"
```

---

## MODULE 4: SoftwareScanner

### Dependencies
```csharp
private readonly IRegistryService _registry;
private readonly ILogger<SoftwareScanner> _logger;
```

### Output Model
```csharp
public class SoftwareInfo
{
    public List<InstalledApp>  Applications  { get; set; } = new();
    public int                 TotalCount    => Applications.Count;
    public long                TotalSizeBytes => Applications.Sum(a => a.EstimatedSizeBytes);

    // Grouped counts
    public int SystemApps      => Applications.Count(a => a.Category == AppCategory.System);
    public int DevApps         => Applications.Count(a => a.Category == AppCategory.Development);
    public int ProductivityApps => Applications.Count(a => a.Category == AppCategory.Productivity);
    public int Bloatware       => Applications.Count(a => a.Category == AppCategory.Bloatware);
    public int TrialApps       => Applications.Count(a => a.LicenseStatus == LicenseStatus.Trial);
    public int ExpiredApps     => Applications.Count(a => a.LicenseStatus == LicenseStatus.Expired);
}

public class InstalledApp
{
    public string        DisplayName        { get; set; } = string.Empty;
    public string        Version            { get; set; } = string.Empty;
    public string        Publisher          { get; set; } = string.Empty;
    public DateTime?     InstallDate        { get; set; }
    public string        InstallLocation    { get; set; } = string.Empty;
    public long          EstimatedSizeBytes { get; set; }
    public string        UninstallString    { get; set; } = string.Empty;
    public AppCategory   Category           { get; set; }
    public LicenseStatus LicenseStatus      { get; set; }
    public string        RegistrySource     { get; set; } = string.Empty; // which hive
    public bool          Is64Bit            { get; set; }
}

public enum AppCategory
{
    System, Development, Productivity, Utilities, Security,
    Media, Games, Bloatware, Unknown
}

public enum LicenseStatus
{
    Licensed, Free, Trial, Expired, Cracked, Unknown
}
```

### Data Sources
```
Registry paths to read (all 3):
  HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*
  HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*
  HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*

Fields per entry:
  DisplayName, DisplayVersion, Publisher, InstallDate, InstallLocation,
  UninstallString, EstimatedSize (in KB)

Deduplication:
  Same DisplayName + same Publisher = duplicate, keep first occurrence

Bloatware detection (keywords in DisplayName):
  "McAfee", "Norton", "Avast", "HP Support", "Dell Support",
  "Lenovo", "Cortana", "Xbox", "Your Phone", "Get Office",
  "Booking.com", "Candy Crush", "TikTok"

Trial detection:
  "trial", "evaluation", "demo", "preview" in DisplayName (case-insensitive)
```

### Score Rules
```
Start at 100. Deduct:
  -5  per expired trial app (max -20)
  -5  per bloatware app (max -15)
  -3  per cracked/unlicensed app (max -15)
  -2  if total app count > 150 (cluttered system)
```

---

## MODULE 5: LicenseAuditor

### Dependencies
```csharp
private readonly IRegistryService _registry;
private readonly IPowerShellService _ps;
private readonly ILogger<LicenseAuditor> _logger;
```

### Output Model
```csharp
public class LicenseInfo
{
    public WindowsLicense     Windows         { get; set; } = new();
    public OfficeLicense?     Office          { get; set; }
    public List<SoftwareLicense> Others       { get; set; } = new();

    public int  LicensedCount   => Others.Count(l => l.Status == LicenseStatus.Licensed);
    public int  FreeCount       => Others.Count(l => l.Status == LicenseStatus.Free);
    public int  TrialCount      => Others.Count(l => l.Status == LicenseStatus.Trial);
    public int  ExpiredCount    => Others.Count(l => l.Status == LicenseStatus.Expired);
    public int  UnknownCount    => Others.Count(l => l.Status == LicenseStatus.Unknown);
    public double CompliancePercent =>
        Others.Count > 0
        ? (LicensedCount + FreeCount) / (double)Others.Count * 100
        : 100;
}

public class WindowsLicense
{
    public string  Edition             { get; set; } = string.Empty;
    public string  ActivationStatus    { get; set; } = string.Empty; // Activated/Not activated
    public string? ProductKey          { get; set; }  // last 5 chars only
    public string  LicenseType         { get; set; } = string.Empty; // OEM/Retail/Volume
    public DateTime? ExpiryDate        { get; set; }
}

public class OfficeLicense
{
    public string  Version             { get; set; } = string.Empty;
    public string  ActivationStatus    { get; set; } = string.Empty;
    public string  LicensedTo          { get; set; } = string.Empty;
    public DateTime? ExpiryDate        { get; set; }
}

public class SoftwareLicense
{
    public string        AppName        { get; set; } = string.Empty;
    public LicenseStatus Status         { get; set; }
    public DateTime?     ExpiryDate     { get; set; }
    public int?          DaysUntilExpiry { get; set; }
    public string        Note           { get; set; } = string.Empty;
}
```

### Data Sources
```
Windows:   PowerShell: (Get-WmiObject SoftwareLicensingProduct | Where {$_.PartialProductKey}).LicenseStatus
           Also: slmgr /dli (parse via PowerShell)

Office:    Registry: HKLM\SOFTWARE\Microsoft\Office\ (find version key)
           HKLM\SOFTWARE\Microsoft\Office\ClickToRun\Configuration
           Key: ProductReleaseIds, CDNBaseUrl

Expiry:    WMI: SoftwareLicensingProduct.LicenseExpirationDate (Windows)
           For Office: parse from registry VersionToReport key
```

### Score Rules
```
Start at 100. Deduct:
  -30 if Windows is not activated
  -20 if Office is not activated (if installed)
  -10 per expired commercial software (max -30)
  -5  per trial software running expired (max -15)
```

### Finding Rules
```
CRITICAL: Windows not activated          → "Windows requires activation"
CRITICAL: Office not activated           → "Microsoft Office requires activation"
WARNING:  License expiring in < 30 days  → "License expiring soon: [app]"
WARNING:  Trial expired                  → "Expired trial still installed: [app]"
INFO:     License expiring in < 90 days  → "License renewal reminder: [app]"
```

---

## MODULE 6: SecurityScanner

### Dependencies
```csharp
private readonly IWmiService _wmi;
private readonly IPowerShellService _ps;
private readonly IRegistryService _registry;
private readonly ILogger<SecurityScanner> _logger;
```

### Output Model
```csharp
public class SecurityStatus
{
    public AntivirusStatus    Antivirus        { get; set; } = new();
    public FirewallStatus     Firewall         { get; set; } = new();
    public DefenderStatus     Defender         { get; set; } = new();
    public BitLockerStatus    BitLocker        { get; set; } = new();
    public UacStatus          Uac              { get; set; } = new();
    public UpdateStatus       WindowsUpdates   { get; set; } = new();
    public SecureBootStatus   SecureBoot       { get; set; } = new();
    public List<string>       RecentErrors     { get; set; } = new(); // Event log
}

public class AntivirusStatus
{
    public string    ProductName       { get; set; } = string.Empty;
    public bool      IsEnabled         { get; set; }
    public bool      IsUpToDate        { get; set; }
    public DateTime? LastUpdateDate    { get; set; }
}

public class DefenderStatus
{
    public bool      RealTimeEnabled   { get; set; }
    public bool      AntivirusEnabled  { get; set; }
    public DateTime? LastScanDate      { get; set; }
    public string    SignatureVersion   { get; set; } = string.Empty;
    public DateTime? SignatureDate     { get; set; }
}

public class FirewallStatus
{
    public bool DomainEnabled  { get; set; }
    public bool PrivateEnabled { get; set; }
    public bool PublicEnabled  { get; set; }
    public bool AllEnabled     => DomainEnabled && PrivateEnabled && PublicEnabled;
}

public class BitLockerStatus
{
    public List<DriveEncryptionStatus> Drives { get; set; } = new();
    public bool AllDrivesEncrypted => Drives.All(d => d.IsEncrypted);
}

public class DriveEncryptionStatus
{
    public string DriveLetter  { get; set; } = string.Empty;
    public bool   IsEncrypted  { get; set; }
    public string Method       { get; set; } = string.Empty; // AES128/AES256
}

public class UacStatus
{
    public bool IsEnabled       { get; set; }
    public int  Level           { get; set; } // 0-3 (registry ConsentPromptBehaviorAdmin)
}

public class UpdateStatus
{
    public DateTime? LastUpdateDate  { get; set; }
    public int       PendingUpdates  { get; set; }
    public List<string> RecentHotfixes { get; set; } = new();
}

public class SecureBootStatus
{
    public bool? IsEnabled      { get; set; }  // null if BIOS doesn't support
    public bool  IsBiosMode     { get; set; }  // true if legacy BIOS (no SecureBoot)
}
```

### Data Sources
```
Antivirus:   WMI: root\SecurityCenter2 → AntiVirusProduct
Defender:    PowerShell: Get-MpComputerStatus (parse fields)
Firewall:    PowerShell: Get-NetFirewallProfile | Select Name,Enabled
BitLocker:   PowerShell: manage-bde -status (parse per drive)
             OR: Get-BitLockerVolume (newer Windows)
UAC:         Registry: HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System
             Value: ConsentPromptBehaviorAdmin (0=disabled, 2=elevated, 5=prompt)
SecureBoot:  PowerShell: Confirm-SecureBootUEFI (catch PlatformNotSupportedException)
Updates:     PowerShell: Get-HotFix | Sort InstalledOn | Select -Last 10
Errors:      EventLog: System + Application, last 7 days, Level=Error, count only
```

### Score Rules
```
Start at 100. Deduct:
  -30 if real-time antivirus disabled
  -25 if firewall disabled on any profile
  -20 if antivirus definitions > 7 days old
  -15 if UAC completely disabled
  -15 if system drive not BitLocker encrypted
  -10 if pending updates > 10
  -10 if SecureBoot disabled (if UEFI)
  -5  if last scan > 7 days ago
```

---

## MODULE 7: StartupScanner

### Dependencies
```csharp
private readonly IRegistryService _registry;
private readonly IPowerShellService _ps;
private readonly ILogger<StartupScanner> _logger;
```

### Output Model
```csharp
public class StartupItem
{
    public string           Name           { get; set; } = string.Empty;
    public string           Command        { get; set; } = string.Empty;
    public string           Location       { get; set; } = string.Empty; // registry path or folder
    public StartupCategory  Category       { get; set; }
    public bool             IsEnabled      { get; set; }
    public double           EstimatedDelaySeconds { get; set; }
    public bool             IsSuspicious   { get; set; }
}

public enum StartupCategory
{
    Essential,   // Windows system, security
    Useful,      // Dev tools, office apps
    Optional,    // Messaging apps, media
    Bloatware,   // OEM software, trials
    Suspicious   // Unknown, unusual path
}
```

### Data Sources
```
Registry Run keys:
  HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run
  HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run
  HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce
  HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce

Startup folders:
  Shell:Startup = C:\Users\[user]\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup
  Shell:Common Startup = C:\ProgramData\Microsoft\Windows\Start Menu\Programs\StartUp

Scheduled Tasks (startup trigger):
  PowerShell: Get-ScheduledTask | Where {$_.Triggers -like "*AtStartup*"}

Services (auto-start non-Microsoft):
  ServiceController.GetServices() where StartType == Automatic
  Filter: exclude common Windows services whitelist
```

### Score Rules
```
Start at 100. Deduct:
  -10 per suspicious startup item
  -5  per bloatware startup item (max -20)
  -5  if total startup items > 30
```

---

## MODULE 8: BrowserScanner

### Dependencies
```csharp
private readonly ILogger<BrowserScanner> _logger;
```

### Output Model
```csharp
public class BrowserExtension
{
    public string         BrowserName     { get; set; } = string.Empty; // Chrome/Edge/Firefox
    public string         ExtensionId     { get; set; } = string.Empty;
    public string         Name            { get; set; } = string.Empty;
    public string         Version         { get; set; } = string.Empty;
    public string         Description     { get; set; } = string.Empty;
    public bool           IsEnabled       { get; set; }
    public List<string>   Permissions     { get; set; } = new();
    public RiskLevel      Risk            { get; set; }
}

public enum RiskLevel { Low, Medium, High }

// The module Data is List<BrowserExtension>
```

### Data Sources
```
Chrome extensions path:
  %LOCALAPPDATA%\Google\Chrome\User Data\Default\Extensions\[id]\[version]\manifest.json
  Read fields: name, version, description, permissions, manifest_version

Edge extensions path:
  %LOCALAPPDATA%\Microsoft\Edge\User Data\Default\Extensions\[id]\[version]\manifest.json

Firefox:
  %APPDATA%\Mozilla\Firefox\Profiles\[profile]\extensions.json
  Parse JSON: "addons" array, fields: name, version, active, permissions

Risk assessment:
  HIGH if permissions contain: tabs, history, cookies, passwords, all URLs (<all_urls>)
  MEDIUM if permissions contain: webRequest, webNavigation, downloads
  LOW otherwise
```

---

## MODULE 9: PerformanceScanner

### Dependencies
```csharp
private readonly IWmiService _wmi;
private readonly ILogger<PerformanceScanner> _logger;
```

### Output Model
```csharp
public class PerformanceMetrics
{
    public double   CpuUsagePercent    { get; set; }
    public double   RamUsagePercent    { get; set; }
    public double   DiskUsagePercent   { get; set; }
    public double   PageFileUsagePercent { get; set; }
    public TimeSpan SystemUptime       { get; set; }
    public DateTime LastBootTime       { get; set; }
    public int      CrashCount7Days    { get; set; }
    public List<TopProcess> TopCpuProcesses { get; set; } = new();
    public List<TopProcess> TopRamProcesses { get; set; } = new();
}

public class TopProcess
{
    public string Name          { get; set; } = string.Empty;
    public int    Pid           { get; set; }
    public double CpuPercent    { get; set; }
    public long   RamBytes      { get; set; }
}
```

### Data Sources
```
CPU Usage:     PerformanceCounter("Processor", "% Processor Time", "_Total")
               Sample twice 1 second apart, return second reading
RAM Usage:     GlobalMemoryStatusEx P/Invoke for accurate numbers
Disk I/O:      PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total")
Page File:     WMI Win32_PageFileUsage (AllocatedBaseSize vs CurrentUsage)
Uptime:        Environment.TickCount64 (milliseconds since boot)
Last Boot:     WMI Win32_OperatingSystem.LastBootUpTime
Crashes:       EventLog query: System log, EventID=41 (unexpected shutdown),
               last 7 days count
Top processes: Process.GetProcesses(), sort by CPU/RAM, take top 5
```

---

## MODULE 10: ThreatDetector

### Dependencies
```csharp
private readonly IRegistryService _registry;
private readonly ILogger<ThreatDetector> _logger;
```

### Output Model
```csharp
public class ThreatScanResult
{
    public List<ThreatFinding>  Threats        { get; set; } = new();
    public int                  FilesScanned   { get; set; }
    public bool                 HostsTampered  { get; set; }
    public List<string>         SuspiciousHosts { get; set; } = new();
    public bool                 ThreatsFound   => Threats.Any(t => t.Severity != Severity.Info);
}

public class ThreatFinding
{
    public string    Name           { get; set; } = string.Empty;
    public string    Type           { get; set; } = string.Empty; // Process/File/Network/Registry
    public string    Detail         { get; set; } = string.Empty;
    public Severity  Severity       { get; set; }
}
```

### Data Sources
```
Malware process names (hardcoded list ~50):
  "cryptominer.exe", "xmrig.exe", "wannacry.exe", "notpetya.exe",
  "mimikatz.exe", "netcat.exe", "ncat.exe", "pwdump.exe", etc.
  → Compare against running Process.GetProcesses()

Hosts file:
  Read C:\Windows\System32\drivers\etc\hosts
  Flag any entry that redirects known sites (google.com, microsoft.com, etc.)
  Flag entries to unusual IPs for known domains

Suspicious autorun paths:
  Check all startup items from StartupScanner
  Flag items running from: %TEMP%, %APPDATA%\Roaming, unusual paths

Recently modified executables:
  System32 folder: look for .exe/.dll modified in last 7 days
  Flag count > 5 as suspicious (potential rootkit)
```

---

## EXPORTER CONTRACTS

### ConsoleReporter
```
Input:  ScanReport
Output: Rich Spectre.Console formatted output to stdout
Shows:  Banner → per-module panels → score table → recommendations
```

### JsonExporter
```
Input:  ScanReport
Output: [hostname]_[timestamp].json
Format: System.Text.Json, indented, camelCase
All:    Every field of every model serialized
```

### HtmlExporter
```
Input:  ScanReport
Output: [hostname]_[timestamp].html
Style:  Dark theme, self-contained (no CDN), all CSS inline
Sections: summary card → hardware → users → network → software →
          licenses → security → startup → browsers → performance →
          threats → recommendations
Charts:   Score gauge as pure CSS (no JS charts dependency)
Tables:   Sortable via vanilla JS (embedded)
```

### CsvExporter
```
Input:  ScanReport
Output: [hostname]_[timestamp]_software.csv (software list)
        [hostname]_[timestamp]_findings.csv (all findings)
Format: UTF-8 with BOM (for Excel compatibility)
```

---
*End of MODULE_CONTRACTS.md*
