# AGENT_PROMPTS.md
# Laptop Forensics Scanner — AI Agent Prompt Sequence
# Version: 1.0 | Author: Kailash Singh Bhakuni
# 
# HOW TO USE:
#   1. Open Cursor / Claude Code / any AI coding agent
#   2. Open project folder in the agent
#   3. Copy-paste PROMPT 0 first (session setup)
#   4. After each prompt completes → update BUILD_STATUS.md
#   5. Then paste next prompt
#   6. NEVER skip a prompt. NEVER combine two prompts.
#   7. If session crashes → use RESUME PROMPT at bottom

---

## ════════════════════════════════════════════════════════
## PROMPT 0 — SESSION SETUP (Paste this FIRST, every time)
## ════════════════════════════════════════════════════════

```
You are a senior C# .NET 8 engineer building a Windows console application.

Before doing anything else, read these 3 files completely:
  1. docs/ARCHITECTURE.md
  2. docs/BUILD_STATUS.md
  3. docs/MODULE_CONTRACTS.md

Rules you must follow throughout this session:
  - Never deviate from patterns defined in ARCHITECTURE.md
  - Never rewrite a file that has ✅ DONE status in BUILD_STATUS.md
  - After completing each file, tell me: "✅ [filename] done"
  - If you are unsure about anything, refer to the MD files first
  - No TODOs, no placeholder comments, no stub implementations
  - Every file must compile cleanly

Confirm you have read all 3 files by saying:
"Ready. Last completed step: [X]. Next step: [Y]."
```

---

## ════════════════════════════════════════════════════════
## PROMPT 1 — Solution Structure & Project Foundation
## ════════════════════════════════════════════════════════

```
Implement STEP 1 from BUILD_STATUS.md.

Create the following files exactly as specified in ARCHITECTURE.md:

1. LaptopForensics.sln
   - Links all 5 projects: Console, Core, Data, Export, Tests

2. src/LaptopForensics.Console/LaptopForensics.Console.csproj
   - TargetFramework: net8.0-windows
   - OutputType: Exe
   - AssemblyName: laptop-forensics
   - PublishSingleFile: true, SelfContained: true, RuntimeIdentifier: win-x64
   - NuGet refs: Spectre.Console 0.49.1, Microsoft.Extensions.DependencyInjection 8.0.0,
     Microsoft.Extensions.Configuration.Json 8.0.0, Serilog 3.1.1,
     Serilog.Sinks.File 5.0.0, Serilog.Sinks.Console 4.1.0
   - Project references: Core, Data, Export

3. src/LaptopForensics.Core/LaptopForensics.Core.csproj
   - TargetFramework: net8.0-windows
   - NuGet refs: System.Management 8.0.0, Microsoft.PowerShell.SDK 7.4.1
   - Enable Nullable

4. src/LaptopForensics.Data/LaptopForensics.Data.csproj
   - TargetFramework: net8.0-windows
   - NuGet refs: System.Data.SQLite 1.0.118
   - Project reference: Core

5. src/LaptopForensics.Export/LaptopForensics.Export.csproj
   - TargetFramework: net8.0-windows
   - NuGet refs: System.Text.Json 8.0.0
   - Project reference: Core

6. tests/LaptopForensics.Tests/LaptopForensics.Tests.csproj
   - TargetFramework: net8.0-windows
   - NuGet refs: xunit 2.7.0, Moq 4.20.70, Microsoft.NET.Test.Sdk 17.10.0
   - Project references: Core, Data, Export

7. src/LaptopForensics.Console/appsettings.json
   - Use exact structure from ARCHITECTURE.md Section 12

8. src/LaptopForensics.Core/GlobalUsings.cs
   - Global usings for: System, System.Collections.Generic, System.Threading.Tasks,
     System.Threading, System.Linq, System.IO, Microsoft.Extensions.Logging,
     LaptopForensics.Core.Models, LaptopForensics.Core.Interfaces

After all files are created, run: dotnet restore LaptopForensics.sln
Report any errors.
```

---

## ════════════════════════════════════════════════════════
## PROMPT 2 — All Interfaces
## ════════════════════════════════════════════════════════

```
Implement STEP 2 from BUILD_STATUS.md.

Create all 7 interface files in src/LaptopForensics.Core/Interfaces/

Use EXACTLY the signatures defined in ARCHITECTURE.md Section 4.
Do not add extra methods. Do not remove any methods.
Add XML doc comments on every interface and method.

Files to create:
  - IScanModule.cs
  - IExporter.cs
  - IWmiService.cs
  - IRegistryService.cs
  - IPowerShellService.cs
  - IScanRepository.cs
  - IScanProgressObserver.cs

After creating all files, run: dotnet build src/LaptopForensics.Core
It must compile with 0 errors.
```

---

## ════════════════════════════════════════════════════════
## PROMPT 3 — All Models
## ════════════════════════════════════════════════════════

```
Implement STEP 3 from BUILD_STATUS.md.

Create all model files in src/LaptopForensics.Core/Models/

Rules:
  - Models are pure data classes — no business logic
  - Use record or class as appropriate (ScanReport = class, rest = record or class)
  - Use required keyword for non-nullable properties
  - Enable nullable reference types
  - No methods except simple computed properties (like TotalRamGb)

Files to create (use exact signatures from ARCHITECTURE.md §5 and MODULE_CONTRACTS.md):
  - ScanReport.cs       ← from ARCHITECTURE.md §5
  - ModuleResult.cs     ← from ARCHITECTURE.md §5
  - Finding.cs          ← includes Severity enum
  - ScanMode.cs         ← [Flags] enum
  - ScanHistory.cs      ← SQLite record (Id, ScanId, Hostname, Timestamp, Score, Grade, ReportJson, DurationMs)
  - HardwareInfo.cs     ← from MODULE_CONTRACTS.md Module 1
  - UserAccountInfo.cs  ← from MODULE_CONTRACTS.md Module 2
  - NetworkInfo.cs      ← from MODULE_CONTRACTS.md Module 3
  - SoftwareInfo.cs     ← from MODULE_CONTRACTS.md Module 4 (includes AppCategory, LicenseStatus enums)
  - LicenseInfo.cs      ← from MODULE_CONTRACTS.md Module 5
  - SecurityStatus.cs   ← from MODULE_CONTRACTS.md Module 6
  - StartupItem.cs      ← from MODULE_CONTRACTS.md Module 7 (includes StartupCategory enum)
  - BrowserExtension.cs ← from MODULE_CONTRACTS.md Module 8 (includes RiskLevel enum)
  - PerformanceMetrics.cs ← from MODULE_CONTRACTS.md Module 9

After creating all files, run: dotnet build src/LaptopForensics.Core
It must compile with 0 errors.
```

---

## ════════════════════════════════════════════════════════
## PROMPT 4 — Services (Infrastructure)
## ════════════════════════════════════════════════════════

```
Implement STEP 4 from BUILD_STATUS.md.

Create 4 service files in src/LaptopForensics.Core/Services/
These services are used by ALL 10 scanners. Get them right.

1. WmiService.cs — implements IWmiService
   - Uses System.Management.ManagementObjectSearcher
   - Query(wmiClass, condition, namespacePath) method:
     → builds WQL: "SELECT * FROM [wmiClass] WHERE [condition]"
     → default namespace: root\cimv2
     → returns IEnumerable<Dictionary<string, object?>>
     → each dict key = property name, value = property value
   - GetSingleValue<T>() calls Query() and returns first result's property
   - ALL exceptions caught: ManagementException, Exception
   - On exception: log warning, return empty enumerable
   - Must handle null property values gracefully

2. RegistryService.cs — implements IRegistryService
   - GetValue(): RegistryKey.OpenBaseKey + OpenSubKey + GetValue
   - GetSubKeyNames(): returns all subkey names under a path
   - GetAllValues(): returns Dictionary of all value names + values under a key
   - KeyExists(): safely checks if key exists
   - ALL methods: catch all exceptions, return null/empty on failure
   - Must handle both HKLM and HKCU hives
   - Must handle 32-bit registry view (RegistryView.Registry32) for WOW6432Node

3. PowerShellService.cs — implements IPowerShellService
   - Uses System.Management.Automation (from Microsoft.PowerShell.SDK)
   - RunAsync(script, timeoutSeconds):
     → Creates PowerShell instance
     → Adds script
     → InvokeAsync with CancellationToken (respect the timeout)
     → Returns all output lines joined as string
     → Catches all exceptions, returns empty string on failure
   - RunAndGetObjectsAsync(): same but returns PSObject collection

4. ElevationService.cs
   - IsRunningAsAdmin(): uses WindowsPrincipal + WindowsBuiltInRole.Administrator
   - RestartAsAdmin(): ProcessStartInfo with Verb = "runas", restarts current exe
   - GetElevationWarning(): returns string message if not admin

After all files, run: dotnet build src/LaptopForensics.Core
0 errors required.
```

---

## ════════════════════════════════════════════════════════
## PROMPT 5 — Helpers
## ════════════════════════════════════════════════════════

```
Implement STEP 5 from BUILD_STATUS.md.

Create 3 helper files in src/LaptopForensics.Core/Helpers/
All helpers are static classes with static methods only.

1. SizeFormatter.cs
   - FormatBytes(long bytes) → "1.23 GB" / "456 MB" / "12 KB" / "500 B"
   - FormatBytesShort(long bytes) → "1.2GB" (no space, 1 decimal)
   - Thresholds: GB=1073741824, MB=1048576, KB=1024

2. ScoreCalculator.cs
   - Calculate(Dictionary<string, ModuleResult> results) → double (0-100)
   - Uses EXACT weights from ARCHITECTURE.md Section 13
   - Only includes modules that are present in results dict (partial scans OK)
   - Normalizes weights if not all modules ran
   - GetGrade(double score) → "EXCELLENT" / "GOOD" / "FAIR" / "POOR"
   - GetGradeColor(string grade) → returns Spectre.Console Color name as string
     EXCELLENT=green, GOOD=blue, FAIR=yellow, POOR=red

3. DateTimeHelper.cs
   - ParseWmiDate(string wmiDate) → DateTime?
     WMI format: "20260531143522.000000+330"
     Use ManagementDateTimeConverter.ToDateTime() with try-catch
   - FormatRelative(DateTime dt) → "2 hours ago" / "3 days ago" / "Just now"
   - FormatDuration(long ms) → "14.3 sec" / "2 min 3 sec"

After all files: dotnet build src/LaptopForensics.Core — 0 errors.
```

---

## ════════════════════════════════════════════════════════
## PROMPT 6 — Data Layer
## ════════════════════════════════════════════════════════

```
Implement STEP 6 from BUILD_STATUS.md.

Create 2 files in src/LaptopForensics.Data/

1. DatabaseContext.cs
   - Constructor takes IOptions<AppSettings> to get DB path
   - EnsureCreated(): creates directory + runs CREATE TABLE IF NOT EXISTS
   - SQL schema EXACTLY from ARCHITECTURE.md Section 11
   - GetConnection(): returns open SQLiteConnection
   - Dispose pattern implemented properly

2. ScanRepository.cs — implements IScanRepository
   - Constructor takes DatabaseContext
   - Calls context.EnsureCreated() in constructor
   - SaveAsync(ScanHistory): INSERT INTO ScanHistory
     → ScanHistory.ReportJson = System.Text.Json.JsonSerializer.Serialize(report)
   - GetAllAsync(): SELECT * ORDER BY ScanTimestamp DESC
   - GetByIdAsync(string scanId): SELECT WHERE ScanId = @scanId
   - GetLatestAsync(): SELECT * ORDER BY ScanTimestamp DESC LIMIT 1
   - DeleteOlderThanAsync(DateTime): DELETE WHERE ScanTimestamp < @cutoff
   - All methods: async, use SQLiteCommand with parameters (no string concat)
   - All exceptions: catch, log, rethrow as ApplicationException with message

After files: dotnet build src/LaptopForensics.Data — 0 errors.
```

---

## ════════════════════════════════════════════════════════
## PROMPT 7A — HardwareScanner
## ════════════════════════════════════════════════════════

```
Implement HardwareScanner.cs in src/LaptopForensics.Core/Scanners/

Read MODULE_CONTRACTS.md Module 1 completely before writing any code.

Requirements:
  - Implements IScanModule
  - ModuleName = "HardwareScanner"
  - ModuleIcon = "🖥️"
  - EstimatedSeconds = 8
  - ApplicableModes = ScanMode.Full | ScanMode.Quick

  ExecuteAsync must:
  1. Query CPU via WMI Win32_Processor (all fields from contract)
  2. Query all RAM slots via WMI Win32_PhysicalMemory
  3. Get free RAM from WMI Win32_OperatingSystem.FreePhysicalMemory
  4. Query disks via WMI Win32_DiskDrive + Win32_LogicalDisk
  5. Try SMART data via DeviceIoControl (graceful fallback if unavailable)
  6. Query GPU via WMI Win32_VideoController
  7. Query battery via WMI Win32_Battery (IsPresent=false if not found)
  8. Query motherboard via WMI Win32_BaseBoard + Win32_BIOS
  9. Calculate score using EXACT rules from MODULE_CONTRACTS.md
  10. Generate findings using EXACT rules from MODULE_CONTRACTS.md
  11. Return ModuleResult with Data = HardwareInfo instance

  Error handling:
  - Each section (CPU/RAM/Disk etc) in its own try-catch
  - One section failing must NOT stop other sections
  - Log warning for each failure, continue

After file: dotnet build src/LaptopForensics.Core — 0 errors.
```

---

## ════════════════════════════════════════════════════════
## PROMPT 7B — UserScanner
## ════════════════════════════════════════════════════════

```
Implement UserScanner.cs in src/LaptopForensics.Core/Scanners/

Read MODULE_CONTRACTS.md Module 2 completely before writing any code.

Requirements:
  - Implements IScanModule
  - ModuleName = "UserScanner"
  - ModuleIcon = "👤"
  - EstimatedSeconds = 10
  - ApplicableModes = ScanMode.Full | ScanMode.Quick

  ExecuteAsync must:
  1. Get all local accounts via WMI Win32_UserAccount (LocalAccount=True)
  2. For each account, get group memberships via WMI Win32_GroupUser
  3. Get last login via WMI Win32_NetworkLoginProfile
  4. Check if account is admin: groups contain "Administrators"
  5. Calculate profile size: DirectoryInfo on C:\Users\[name] recursive
     (catch UnauthorizedAccessException per folder)
  6. Get active sessions: WMI Win32_LogonSession + Win32_LoggedOnUser
  7. Get login history: EventLog("Security") filter EventID 4624 + 4625
     Last 50 events only. Catch if Security log not accessible.
  8. Get password policy: PowerShell "net accounts" → parse output
  9. Calculate score using EXACT rules from MODULE_CONTRACTS.md
  10. Generate findings using EXACT rules from MODULE_CONTRACTS.md

After file: dotnet build src/LaptopForensics.Core — 0 errors.
```

---

## ════════════════════════════════════════════════════════
## PROMPT 7C — NetworkScanner
## ════════════════════════════════════════════════════════

```
Implement NetworkScanner.cs in src/LaptopForensics.Core/Scanners/

Read MODULE_CONTRACTS.md Module 3 completely before writing any code.

Requirements:
  - Implements IScanModule
  - ModuleName = "NetworkScanner"
  - ModuleIcon = "🌐"
  - EstimatedSeconds = 12

  ExecuteAsync must:
  1. Get all adapters via NetworkInterface.GetAllNetworkInterfaces()
     Extract: IP, subnet, gateway, DNS, MAC, speed, bytes sent/received
     Detect type: Ethernet / WiFi / VPN / Loopback
  2. Get active TCP connections via IPGlobalProperties.GetActiveTcpConnections()
  3. Map each connection to process via P/Invoke GetExtendedTcpTable
     (use MIB_TCPTABLE2 structure — implement full P/Invoke)
  4. For each unique remote IP: Dns.GetHostEntry() with 1000ms timeout
     Run hostname lookups in parallel (Task.WhenAll, max 10 concurrent)
  5. Get UDP listeners via GetActiveUdpListeners()
  6. Get saved WiFi networks via PowerShell: "netsh wlan show profiles"
     Parse profile names from output
  7. Ping 8.8.8.8 five times, calculate average/min/max/packetloss
  8. Get firewall rule count via PowerShell: "netsh advfirewall show allprofiles"
  9. Flag suspicious connections (see scoring rules in contract)
  10. Calculate score and generate findings per MODULE_CONTRACTS.md

After file: dotnet build src/LaptopForensics.Core — 0 errors.
```

---

## ════════════════════════════════════════════════════════
## PROMPT 7D — SoftwareScanner
## ════════════════════════════════════════════════════════

```
Implement SoftwareScanner.cs in src/LaptopForensics.Core/Scanners/

Read MODULE_CONTRACTS.md Module 4 completely before writing any code.

Requirements:
  - Implements IScanModule
  - ModuleName = "SoftwareScanner"
  - ModuleIcon = "📦"
  - EstimatedSeconds = 8

  ExecuteAsync must:
  1. Read from ALL 3 registry paths:
     HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*
     HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\* (32-bit view)
     HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*
  2. Skip entries with no DisplayName
  3. Deduplicate: same DisplayName + Publisher = keep first
  4. Parse InstallDate: format is "20240615" → DateTime
  5. EstimatedSize in registry is in KB → convert to bytes
  6. Categorize each app (use keyword matching):
     System: "Microsoft", "Windows", "Visual C++", ".NET"
     Development: "Visual Studio", "Git", "Docker", "Python", "Node", "JetBrains"
     Productivity: "Office", "Teams", "Slack", "Zoom", "Chrome", "Firefox"
     Bloatware: "McAfee", "Norton", "HP Support", "Dell", "Candy Crush", etc.
     (full list in MODULE_CONTRACTS.md)
  7. Set LicenseStatus:
     Trial: "trial"/"evaluation"/"demo" in DisplayName
     Expired: trial AND (InstallDate + 30 days) < today
     Free: known open source apps
     Licensed: default for commercial apps
  8. Calculate score and findings per MODULE_CONTRACTS.md

After file: dotnet build src/LaptopForensics.Core — 0 errors.
```

---

## ════════════════════════════════════════════════════════
## PROMPT 7E — LicenseAuditor
## ════════════════════════════════════════════════════════

```
Implement LicenseAuditor.cs in src/LaptopForensics.Core/Scanners/

Read MODULE_CONTRACTS.md Module 5 completely before writing any code.

Requirements:
  - Implements IScanModule
  - ModuleName = "LicenseAuditor"
  - ModuleIcon = "📋"
  - EstimatedSeconds = 10

  ExecuteAsync must:
  1. Check Windows activation:
     PowerShell: Get-WmiObject SoftwareLicensingProduct |
       Where-Object {$_.PartialProductKey -and $_.ApplicationId -eq '55c92734-d682-4d71-983e-d6ec3f16059f'} |
       Select LicenseStatus, Description, PartialProductKey, LicenseExpirationDate
     LicenseStatus 1 = Licensed, others = not activated
  2. Check Office:
     Registry: HKLM\SOFTWARE\Microsoft\Office\ClickToRun\Configuration
     Keys: ProductReleaseIds, UpdateChannel, VersionToReport
     If found: PowerShell check activation
  3. Get all installed apps from SoftwareScanner results
     (re-read registry or accept as dependency — use registry directly)
  4. For each commercial app: determine license status
  5. Calculate days until expiry for expiring licenses
  6. Calculate compliance percentage
  7. Score and findings per MODULE_CONTRACTS.md

After file: dotnet build src/LaptopForensics.Core — 0 errors.
```

---

## ════════════════════════════════════════════════════════
## PROMPT 7F — SecurityScanner
## ════════════════════════════════════════════════════════

```
Implement SecurityScanner.cs in src/LaptopForensics.Core/Scanners/

Read MODULE_CONTRACTS.md Module 6 completely before writing any code.

Requirements:
  - Implements IScanModule
  - ModuleName = "SecurityScanner"
  - ModuleIcon = "🔒"
  - EstimatedSeconds = 12
  - ApplicableModes = ScanMode.Full | ScanMode.Quick

  ExecuteAsync must:
  1. Antivirus: WMI root\SecurityCenter2 → AntiVirusProduct
     Fields: displayName, productState (parse hex for enabled/upToDate)
     productState parsing: byte 12 = enabled (0x10=enabled, 0x00=disabled)
                           byte 10 = upToDate (0x00=updated, 0x10=outdated)
  2. Windows Defender: PowerShell Get-MpComputerStatus
     Parse: AMServiceEnabled, AntispywareEnabled, RealTimeProtectionEnabled,
            AntivirusSignatureLastUpdated, QuickScanStartTime
  3. Firewall: PowerShell Get-NetFirewallProfile | Select Name,Enabled
     Parse Domain/Private/Public profile enabled status
  4. BitLocker: PowerShell manage-bde -status
     Parse per drive: Protection Status, Encryption Method
     Fallback: Get-BitLockerVolume if manage-bde fails
  5. UAC: Registry HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System
     Value: EnableLUA (0=disabled, 1=enabled)
     Value: ConsentPromptBehaviorAdmin (0-5, level of UAC prompt)
  6. SecureBoot: PowerShell Confirm-SecureBootUEFI
     Catch PlatformNotSupportedException (legacy BIOS)
  7. Windows Updates: PowerShell Get-HotFix | Sort-Object InstalledOn -Descending
     Take last 10. Extract last install date.
  8. Event log errors last 7 days:
     EventLog("System") + EventLog("Application") where EntryType == Error
     Count only — do not load all messages
  9. Score and findings per MODULE_CONTRACTS.md

After file: dotnet build src/LaptopForensics.Core — 0 errors.
```

---

## ════════════════════════════════════════════════════════
## PROMPT 7G — StartupScanner
## ════════════════════════════════════════════════════════

```
Implement StartupScanner.cs in src/LaptopForensics.Core/Scanners/

Read MODULE_CONTRACTS.md Module 7 completely before writing any code.

Requirements:
  - Implements IScanModule
  - ModuleName = "StartupScanner"
  - ModuleIcon = "⚡"
  - EstimatedSeconds = 6

  ExecuteAsync must:
  1. Registry Run keys (4 locations):
     HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run
     HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run
     HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce
     HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce
  2. Startup folders (2 locations):
     Environment.GetFolderPath(SpecialFolder.Startup)
     Environment.GetFolderPath(SpecialFolder.CommonStartup)
     List all .lnk and .exe files
  3. Scheduled tasks with startup trigger:
     PowerShell: Get-ScheduledTask | Where-Object {$_.Triggers -match 'Boot|Logon'}
     Parse: TaskName, TaskPath, State, Actions
  4. Categorize each item (Essential/Useful/Optional/Bloatware/Suspicious):
     Essential: Windows Defender, SecurityHealth, OneDrive (system)
     Bloatware: HP/Dell/Lenovo OEM tools, McAfee, Cortana
     Suspicious: items running from %TEMP%, %APPDATA%, unusual paths
  5. Estimate startup delay: assign 0.5-3.0 seconds per item based on category
  6. Score and findings per MODULE_CONTRACTS.md

After file: dotnet build src/LaptopForensics.Core — 0 errors.
```

---

## ════════════════════════════════════════════════════════
## PROMPT 7H — BrowserScanner
## ════════════════════════════════════════════════════════

```
Implement BrowserScanner.cs in src/LaptopForensics.Core/Scanners/

Read MODULE_CONTRACTS.md Module 8 completely before writing any code.

Requirements:
  - Implements IScanModule
  - ModuleName = "BrowserScanner"
  - ModuleIcon = "🌍"
  - EstimatedSeconds = 5

  ExecuteAsync must:
  1. Chrome extensions:
     Path: Environment.GetFolderPath(LocalApplicationData) +
           \Google\Chrome\User Data\Default\Extensions\
     For each subfolder (extension ID): find latest version subfolder
     Read manifest.json: parse name, version, description, permissions array
  2. Edge extensions:
     Same structure but: \Microsoft\Edge\User Data\Default\Extensions\
  3. Firefox extensions:
     Path: Environment.GetFolderPath(ApplicationData) +
           \Mozilla\Firefox\Profiles\
     Find first profile folder, read extensions.json
     Parse JSON: addons[] array → name, version, active, userPermissions.permissions
  4. Risk assessment per extension:
     HIGH: permissions contains "<all_urls>", "tabs", "history", "cookies",
           "passwords", "webAuthenticationProxy"
     MEDIUM: "webRequest", "webNavigation", "downloads", "management"
     LOW: everything else
  5. Handle missing browser gracefully (browser not installed = skip, no error)
  6. Score: -10 per HIGH risk extension (max -40), -5 per MEDIUM (max -20)
  7. Findings: flag every HIGH risk extension as Warning

After file: dotnet build src/LaptopForensics.Core — 0 errors.
```

---

## ════════════════════════════════════════════════════════
## PROMPT 7I — PerformanceScanner
## ════════════════════════════════════════════════════════

```
Implement PerformanceScanner.cs in src/LaptopForensics.Core/Scanners/

Read MODULE_CONTRACTS.md Module 9 completely before writing any code.

Requirements:
  - Implements IScanModule
  - ModuleName = "PerformanceScanner"
  - ModuleIcon = "📊"
  - EstimatedSeconds = 5

  ExecuteAsync must:
  1. CPU usage:
     new PerformanceCounter("Processor", "% Processor Time", "_Total")
     Call NextValue(), await Task.Delay(1000), call NextValue() again
     Second reading is accurate
  2. RAM usage:
     P/Invoke GlobalMemoryStatusEx (MEMORYSTATUSEX struct)
     Fields: dwMemoryLoad, ullTotalPhys, ullAvailPhys
  3. Disk I/O:
     new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total")
     Same two-reading pattern as CPU
  4. Page file:
     WMI Win32_PageFileUsage: AllocatedBaseSize, CurrentUsage
  5. Uptime:
     Environment.TickCount64 / 1000 → seconds → TimeSpan
  6. Last boot:
     WMI Win32_OperatingSystem.LastBootUpTime → parse via DateTimeHelper
  7. System crashes (last 7 days):
     EventLog("System"), EventID == 41 (kernel power) OR EventID == 1001
     Count only
  8. Top 5 CPU processes:
     Process.GetProcesses() — use PerformanceCounter per process
     WARNING: this is slow. Sample top processes by WorkingSet64 as RAM proxy.
     For CPU: sort by TotalProcessorTime delta (sample twice 500ms apart)
  9. Score: 100 base
     -20 if CPU > 90%, -10 if CPU > 70%
     -20 if RAM > 90%, -10 if RAM > 80%
     -10 if crashes > 3 in 7 days
     -5  if disk I/O > 90%

After file: dotnet build src/LaptopForensics.Core — 0 errors.
```

---

## ════════════════════════════════════════════════════════
## PROMPT 7J — ThreatDetector
## ════════════════════════════════════════════════════════

```
Implement ThreatDetector.cs in src/LaptopForensics.Core/Scanners/

Read MODULE_CONTRACTS.md Module 10 completely before writing any code.

Requirements:
  - Implements IScanModule
  - ModuleName = "ThreatDetector"
  - ModuleIcon = "🛡️"
  - EstimatedSeconds = 8

  ExecuteAsync must:
  1. Known malware process check:
     Hardcode list of 50+ known malware/hacking tool process names:
     "xmrig", "cryptominer", "mimikatz", "pwdump", "wce", "fgdump",
     "gsecdump", "cachedump", "lsadump", "procdump", "netcat", "ncat",
     "psexec", "wmiexec", "dcomexec", "smbexec", "winexe",
     "wannacry", "notpetya", "petya", "locky", "cerber",
     "darkcomet", "njrat", "remcos", "nanocore", "quasar",
     "cobaltstrike", "metasploit", "meterpreter",
     "keylogger", "spyware", "rootkit", "backdoor",
     "torrent" (optional), "bitcoin", "monero"
     Compare (case-insensitive, partial match) against running processes
  2. Hosts file check:
     Read C:\Windows\System32\drivers\etc\hosts
     Flag any non-comment line that redirects:
       known domains (google.com, microsoft.com, windows.com, etc.) to non-standard IPs
     Flag any line redirecting to 0.0.0.0 for > 5 entries (possible ad blocker, mark as Info)
  3. Suspicious autorun paths:
     Get startup items (re-read registry Run keys)
     Flag items with command containing: %TEMP%, %TMP%, \AppData\Local\Temp\,
     C:\Users\Public\, unusual file extensions (.vbs, .ps1, .bat in startup)
  4. Recently modified system files:
     Check C:\Windows\System32\ for .exe or .dll files
     modified in last 7 days
     Count them. If count > 10: Warning
     If count > 50: Critical (possible rootkit/patch)
  5. Score: 100 base
     -50 per known malware process found
     -30 if hosts file tampered with known domains
     -20 if suspicious autorun paths found
     -15 if system files modified count > 50
     -5  if system files modified count > 10
  6. All Critical/Warning findings clearly describe what was found

After file: dotnet build src/LaptopForensics.Core — 0 errors.
```

---

## ════════════════════════════════════════════════════════
## PROMPT 8 — Orchestration Layer
## ════════════════════════════════════════════════════════

```
Implement STEP 8 from BUILD_STATUS.md.

Create 3 files in src/LaptopForensics.Core/Orchestration/

1. TimedScanModule.cs — Decorator pattern
   - Wraps any IScanModule
   - Constructor: (IScanModule inner, IScanProgressObserver observer)
   - Forwards all IScanModule properties to inner
   - ExecuteAsync():
     → Call observer.OnModuleStarted(moduleName, icon, estimatedSeconds)
     → Start Stopwatch
     → await inner.ExecuteAsync(ct)
     → Stop Stopwatch
     → Override DurationMs in result with actual elapsed
     → Call observer.OnModuleCompleted(moduleName, result)
     → Return result
   - If inner throws: catch, create failed ModuleResult, notify observer, return it

2. ScannerFactory.cs — Factory pattern
   - Create(ScanMode mode, IEnumerable<IScanModule> all) → IEnumerable<IScanModule>
   - Full: return all 10
   - Quick: return only where ApplicableModes has Quick flag
   - Single: accept module name string, return matching one only
   - Wrap each returned module in TimedScanModule

3. ScanOrchestrator.cs — Builder pattern
   - Constructor: (IEnumerable<IScanModule> modules, IScanRepository repo,
                   ScannerFactory factory, IScanProgressObserver observer,
                   IOptions<AppSettings> settings, ILogger<ScanOrchestrator> logger)
   - RunAsync(ScanMode mode, string? singleModuleName, CancellationToken ct):
     → Create new ScanReport
     → Get modules from factory
     → For each module: await module.ExecuteAsync(ct)
       → Add ModuleResult to report.Results dict
       → Never let one module failure stop others
     → Set report.TotalDurationMs
     → If settings.Storage enabled: await repo.SaveAsync(ScanHistory from report)
     → Call observer.OnScanCompleted(report)
     → Return ScanReport

After all files: dotnet build src/LaptopForensics.Core — 0 errors.
```

---

## ════════════════════════════════════════════════════════
## PROMPT 9 — Export Layer
## ════════════════════════════════════════════════════════

```
Implement STEP 9 from BUILD_STATUS.md.

Create 4 files in src/LaptopForensics.Export/
Read MODULE_CONTRACTS.md "EXPORTER CONTRACTS" section before starting.

1. JsonExporter.cs — implements IExporter
   FormatName = "JSON", FileExtension = ".json"
   - Serialize entire ScanReport using System.Text.Json
   - Options: WriteIndented=true, PropertyNamingPolicy=CamelCase
   - Filename: [Hostname]_[yyyyMMdd_HHmmss].json
   - Handle circular references with ReferenceHandler.Preserve

2. CsvExporter.cs — implements IExporter
   FormatName = "CSV", FileExtension = ".csv"
   - Export TWO files:
     a. [hostname]_[timestamp]_software.csv
        Columns: Name, Version, Publisher, InstallDate, SizeGB, Category, LicenseStatus
     b. [hostname]_[timestamp]_findings.csv
        Columns: Module, Severity, Title, Description, Recommendation
   - UTF-8 with BOM (new UTF8Encoding(true))
   - Escape commas and quotes in values

3. HtmlExporter.cs — implements IExporter
   FormatName = "HTML", FileExtension = ".html"
   - Self-contained: all CSS inline, no external CDN
   - Dark theme: background #0d1117, text #c9d1d9, accent #1f6feb
   - Structure:
     → Header: hostname, OS, scan date, overall score badge
     → Summary cards: one per module (score + grade)
     → Sections: Hardware → Users → Network → Software → Licenses →
                 Security → Startup → Browsers → Performance → Threats
     → Findings table: sortable by severity (vanilla JS, embedded)
     → Recommendations list: numbered, colored by severity
   - Score badge: CSS circle, color by grade
   - All tables: striped rows, hover highlight
   - Filename: [Hostname]_[yyyyMMdd_HHmmss].html

4. ConsoleReporter.cs — implements IExporter AND IScanProgressObserver
   FormatName = "Console", FileExtension = ""
   ExportAsync(): renders full ScanReport to console using Spectre.Console
   - Banner: ASCII art "LAPTOP FORENSICS" in figlet
   - Per module: Spectre Panel with title, score bar, top 3 findings
   - Summary table: all modules with score + grade colored
   - Overall score: big Rule with colored grade text
   - Recommendations: numbered list, color-coded
   IScanProgressObserver implementation:
   - OnModuleStarted: show spinner with module name
   - OnModuleCompleted: replace spinner with ✅/⚠️ + score
   - OnScanCompleted: trigger ExportAsync

After all files: dotnet build src/LaptopForensics.Export — 0 errors.
```

---

## ════════════════════════════════════════════════════════
## PROMPT 10 — Console Entry Point (Final Step)
## ════════════════════════════════════════════════════════

```
Implement STEP 10 from BUILD_STATUS.md.

Create 3 files in src/LaptopForensics.Console/

1. ProgressTracker.cs
   - Tracks per-module: start time, estimated seconds, actual seconds
   - CalculateEta(int completedModules, int totalModules) → TimeSpan
   - FormatEta(TimeSpan eta) → "~2 min remaining" / "~30 sec remaining"

2. ConsoleUI.cs
   - Implements IScanProgressObserver
   - Uses Spectre.Console AnsiConsole throughout
   - ShowBanner(): figlet "FORENSICS" + subtitle + hostname + OS version
   - ShowScanStarting(ScanMode mode, int moduleCount): inform user
   - OnModuleStarted(): Spectre.Console Status spinner per module
   - OnModuleCompleted(): update live display with result
   - ShowHistory(IEnumerable<ScanHistory>): table with past scans
   - ShowHelp(): formatted help text

3. Program.cs
   - Check admin: ElevationService.IsRunningAsAdmin()
     If not admin: show warning but continue (some data will be incomplete)
   - Parse CLI args manually or use System.CommandLine:
     --quick, --module [name], --export [format], --output [path],
     --silent, --watch, --interval, --history, --help, --version
   - Setup Serilog from appsettings.json
   - Setup DI container (all services, scanners, exporters, orchestrator)
   - Load appsettings.json via Microsoft.Extensions.Configuration
   - Run appropriate action based on args:
     --history → repo.GetAllAsync() → ConsoleUI.ShowHistory()
     --help    → ConsoleUI.ShowHelp()
     --watch   → loop: await orchestrator.RunAsync(), delay interval
     default   → await orchestrator.RunAsync(mode, moduleName, ct)
   - Export to requested formats after scan
   - Exit code: 0 = success, 1 = error, 2 = threats found

After all files:
  dotnet build LaptopForensics.sln — 0 errors, 0 warnings
```

---

## ════════════════════════════════════════════════════════
## PROMPT 11 — Build Script & README
## ════════════════════════════════════════════════════════

```
Implement STEP 11 from BUILD_STATUS.md.

1. Create build.ps1 in project root:
   - Checks .NET 8 SDK is installed, error if not
   - dotnet restore LaptopForensics.sln
   - dotnet build LaptopForensics.sln -c Release
   - If build fails: exit with error message
   - dotnet publish src/LaptopForensics.Console -c Release -r win-x64
     /p:PublishSingleFile=true /p:SelfContained=true
     /p:IncludeNativeLibrariesForSelfExtract=true
     /p:EnableCompressionInSingleFile=true
     -o .\publish\
   - Show final exe size
   - Show: "✅ Build complete: .\publish\laptop-forensics.exe"

2. Create README.md in project root:
   - Project overview (2-3 lines)
   - Prerequisites: .NET 8 SDK, Windows 10/11, Admin rights
   - Build instructions (build.ps1 usage)
   - CLI usage with examples (all flags)
   - Output files location
   - Phase 2 note
   - Architecture overview link (docs/ARCHITECTURE.md)

Final verification — run these and confirm all pass:
  dotnet build LaptopForensics.sln -c Release
  dotnet run --project src/LaptopForensics.Console -- --help
  dotnet run --project src/LaptopForensics.Console -- --quick

Report final status.
```

---

## ════════════════════════════════════════════════════════
## RESUME PROMPT — Use if session crashes mid-way
## ════════════════════════════════════════════════════════

```
I am resuming the LaptopForensics project after a session interruption.

Read these files in order before doing anything:
  1. docs/ARCHITECTURE.md      — full system design
  2. docs/BUILD_STATUS.md      — check what is ✅ DONE vs ⬜ PENDING
  3. docs/MODULE_CONTRACTS.md  — module contracts

After reading, tell me:
  - Last ✅ DONE file
  - Next ⬜ PENDING file
  - Any ❌ BROKEN files that need fixing

Then continue from exactly where we left off.
Do NOT rewrite or re-create any file marked ✅ DONE.
Do NOT ask for confirmation — just start the next pending task.
```

---

## ════════════════════════════════════════════════════════
## FIX PROMPT — Use if a file has compile errors
## ════════════════════════════════════════════════════════

```
File [FILENAME] has compile errors. Here are the errors:

[PASTE ERRORS HERE]

Fix only this file. Do not touch any other file.
After fixing, run: dotnet build src/LaptopForensics.Core
Confirm 0 errors.
Then update BUILD_STATUS.md: change [FILENAME] from ❌ BROKEN to ✅ DONE.
```

---

## ════════════════════════════════════════════════════════
## QUICK REFERENCE — Prompt Order
## ════════════════════════════════════════════════════════

```
0  → Session Setup (ALWAYS first)
1  → Solution + .csproj files
2  → All 7 Interfaces
3  → All 14 Models
4  → 4 Services
5  → 3 Helpers
6  → Data Layer (SQLite)
7A → HardwareScanner
7B → UserScanner
7C → NetworkScanner
7D → SoftwareScanner
7E → LicenseAuditor
7F → SecurityScanner
7G → StartupScanner
7H → BrowserScanner
7I → PerformanceScanner
7J → ThreatDetector
8  → Orchestration (Decorator + Factory + Orchestrator)
9  → Export Layer (JSON + CSV + HTML + Console)
10 → Console Entry Point (Program.cs)
11 → Build Script + README

Total: 21 prompts → Complete Phase 1
```

---
*End of AGENT_PROMPTS.md*
