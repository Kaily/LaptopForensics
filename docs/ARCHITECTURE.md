# ARCHITECTURE.md
# Laptop Forensics Scanner — System Design Reference
# Version: 1.0 | Last Updated: 2026-05-31
# Author: Kailash Singh Bhakuni

---

## ⚠️ AGENT INSTRUCTION
> Read this file COMPLETELY before writing any code.
> If you are resuming mid-session, read BUILD_STATUS.md next.
> Never deviate from patterns defined here without updating this file first.

---

## 1. PROJECT IDENTITY

```
Project Name   : LaptopForensics
Type           : Windows Console Application (.exe)
Language       : C# (.NET 8)
Target OS      : Windows 10 (1903+) / Windows 11 x64
Output         : Single self-contained .exe (~35 MB)
Entry Point    : src/LaptopForensics.Console/Program.cs
Solution File  : LaptopForensics.sln
```

---

## 2. SOLUTION STRUCTURE

```
LaptopForensics/
│
├── LaptopForensics.sln
│
├── docs/
│   ├── ARCHITECTURE.md          ← YOU ARE HERE
│   ├── BUILD_STATUS.md          ← Track progress here
│   └── MODULE_CONTRACTS.md      ← Every module's contract
│
├── src/
│   ├── LaptopForensics.Console/       ← Presentation Layer
│   │   ├── LaptopForensics.Console.csproj
│   │   ├── Program.cs                 ← Entry point, DI setup, UAC check
│   │   ├── ConsoleUI.cs               ← All Spectre.Console rendering
│   │   ├── ProgressTracker.cs         ← Progress bars, module timing
│   │   └── appsettings.json           ← Runtime config
│   │
│   ├── LaptopForensics.Core/          ← Business Logic Layer
│   │   ├── LaptopForensics.Core.csproj
│   │   ├── Models/
│   │   │   ├── ScanReport.cs          ← Root aggregate, holds all results
│   │   │   ├── ModuleResult.cs        ← Output of each scanner
│   │   │   ├── Finding.cs             ← Individual issue/warning/info
│   │   │   ├── HardwareInfo.cs
│   │   │   ├── UserAccountInfo.cs
│   │   │   ├── NetworkInfo.cs
│   │   │   ├── SoftwareInfo.cs
│   │   │   ├── LicenseInfo.cs
│   │   │   ├── SecurityStatus.cs
│   │   │   ├── StartupItem.cs
│   │   │   ├── BrowserExtension.cs
│   │   │   └── PerformanceMetrics.cs
│   │   │
│   │   ├── Interfaces/
│   │   │   ├── IScanModule.cs         ← ALL scanners implement this
│   │   │   ├── IExporter.cs           ← ALL exporters implement this
│   │   │   ├── IWmiService.cs
│   │   │   ├── IRegistryService.cs
│   │   │   ├── IPowerShellService.cs
│   │   │   ├── IScanRepository.cs
│   │   │   └── IScanProgressObserver.cs
│   │   │
│   │   ├── Scanners/
│   │   │   ├── HardwareScanner.cs
│   │   │   ├── UserScanner.cs
│   │   │   ├── NetworkScanner.cs
│   │   │   ├── SoftwareScanner.cs
│   │   │   ├── LicenseAuditor.cs
│   │   │   ├── SecurityScanner.cs
│   │   │   ├── StartupScanner.cs
│   │   │   ├── BrowserScanner.cs
│   │   │   ├── PerformanceScanner.cs
│   │   │   └── ThreatDetector.cs
│   │   │
│   │   ├── Services/
│   │   │   ├── WmiService.cs          ← All WMI queries go through here
│   │   │   ├── RegistryService.cs     ← All Registry reads go through here
│   │   │   ├── PowerShellService.cs   ← All PS script execution here
│   │   │   └── ElevationService.cs    ← UAC elevation check + restart
│   │   │
│   │   ├── Helpers/
│   │   │   ├── SizeFormatter.cs       ← bytes → KB/MB/GB string
│   │   │   ├── ScoreCalculator.cs     ← Module scores → overall score
│   │   │   └── DateTimeHelper.cs      ← WMI date string → DateTime
│   │   │
│   │   └── Orchestration/
│   │       ├── ScanOrchestrator.cs    ← Runs all modules, builds ScanReport
│   │       ├── ScannerFactory.cs      ← Returns modules based on ScanMode
│   │       └── TimedScanModule.cs     ← Decorator: wraps IScanModule + timing
│   │
│   ├── LaptopForensics.Data/          ← Data Layer
│   │   ├── LaptopForensics.Data.csproj
│   │   ├── DatabaseContext.cs         ← SQLite connection management
│   │   └── ScanRepository.cs         ← Implements IScanRepository
│   │
│   └── LaptopForensics.Export/        ← Export Layer
│       ├── LaptopForensics.Export.csproj
│       ├── ConsoleReporter.cs
│       ├── JsonExporter.cs
│       ├── HtmlExporter.cs
│       └── CsvExporter.cs
│
├── tests/
│   └── LaptopForensics.Tests/
│       ├── LaptopForensics.Tests.csproj
│       ├── Scanners/                  ← Unit tests per scanner
│       └── Mocks/                     ← IWmiService, IRegistryService mocks
│
├── build.ps1                          ← One-click publish script
└── README.md
```

---

## 3. LAYER RESPONSIBILITIES

```
Console Layer    → CLI parsing, DI wiring, UAC elevation, rendering only
                   NEVER contains business logic

Core Layer       → All scan logic, models, interfaces, orchestration
                   NEVER references Console or Data layers directly

Data Layer       → SQLite only. Implements IScanRepository.
                   NEVER contains business logic

Export Layer     → Implements IExporter for each format
                   Takes ScanReport as input, produces output file
                   NEVER calls scanners directly
```

**Golden Rule: Dependencies flow INWARD only.**
```
Console → Core ← Data
Export  → Core
```
Core never imports Console, Data, or Export.

---

## 4. ALL INTERFACES (Copy these exactly)

### IScanModule.cs
```csharp
namespace LaptopForensics.Core.Interfaces;

public interface IScanModule
{
    string ModuleName       { get; }
    string ModuleIcon       { get; }  // e.g. "🖥️" for display
    int    EstimatedSeconds { get; }  // used for progress bar estimation
    ScanMode ApplicableModes { get; } // Full, Quick, or Both

    Task<ModuleResult> ExecuteAsync(CancellationToken ct);
}
```

### IExporter.cs
```csharp
namespace LaptopForensics.Core.Interfaces;

public interface IExporter
{
    string FormatName { get; }        // "JSON", "HTML", "CSV"
    string FileExtension { get; }     // ".json", ".html", ".csv"

    Task ExportAsync(ScanReport report, string outputPath);
}
```

### IWmiService.cs
```csharp
namespace LaptopForensics.Core.Interfaces;

public interface IWmiService
{
    IEnumerable<Dictionary<string, object?>> Query(string wmiClass, string? condition = null, string? namespacePath = null);
    T? GetSingleValue<T>(string wmiClass, string property, string? condition = null);
}
```

### IRegistryService.cs
```csharp
namespace LaptopForensics.Core.Interfaces;

public interface IRegistryService
{
    object? GetValue(RegistryHive hive, string keyPath, string valueName);
    IEnumerable<string> GetSubKeyNames(RegistryHive hive, string keyPath);
    Dictionary<string, object?> GetAllValues(RegistryHive hive, string keyPath);
    bool KeyExists(RegistryHive hive, string keyPath);
}
```

### IPowerShellService.cs
```csharp
namespace LaptopForensics.Core.Interfaces;

public interface IPowerShellService
{
    Task<string> RunAsync(string script, int timeoutSeconds = 10);
    Task<IEnumerable<PSObject>> RunAndGetObjectsAsync(string script, int timeoutSeconds = 10);
}
```

### IScanRepository.cs
```csharp
namespace LaptopForensics.Core.Interfaces;

public interface IScanRepository
{
    Task SaveAsync(ScanHistory record);
    Task<IEnumerable<ScanHistory>> GetAllAsync();
    Task<ScanHistory?> GetByIdAsync(string scanId);
    Task<ScanHistory?> GetLatestAsync();
    Task DeleteOlderThanAsync(DateTime cutoff);
}
```

### IScanProgressObserver.cs
```csharp
namespace LaptopForensics.Core.Interfaces;

public interface IScanProgressObserver
{
    void OnModuleStarted(string moduleName, string icon, int estimatedSeconds);
    void OnModuleCompleted(string moduleName, ModuleResult result);
    void OnScanCompleted(ScanReport report);
}
```

---

## 5. CORE MODELS (Exact signatures)

### ModuleResult.cs
```csharp
namespace LaptopForensics.Core.Models;

public record ModuleResult
{
    public required string   ModuleName    { get; init; }
    public required bool     Success       { get; init; }
    public required double   Score         { get; init; }   // 0-100
    public required string   Grade         { get; init; }   // EXCELLENT/GOOD/FAIR/POOR
    public required List<Finding> Findings { get; init; }
    public required object   Data          { get; init; }   // Module-specific POCO
    public required long     DurationMs    { get; init; }
    public string?           ErrorMessage  { get; init; }   // set if Success=false
}
```

### Finding.cs
```csharp
namespace LaptopForensics.Core.Models;

public record Finding
{
    public required Severity Level          { get; init; }
    public required string   Title         { get; init; }
    public required string   Description   { get; init; }
    public required string   Recommendation { get; init; }
}

public enum Severity { Info, Warning, Critical }
```

### ScanReport.cs
```csharp
namespace LaptopForensics.Core.Models;

public class ScanReport
{
    public string   ScanId        { get; } = Guid.NewGuid().ToString();
    public DateTime ScanTimestamp { get; } = DateTime.Now;
    public string   Hostname      { get; set; } = Environment.MachineName;
    public string   OsVersion     { get; set; } = string.Empty;
    public ScanMode Mode          { get; set; }

    public Dictionary<string, ModuleResult> Results { get; } = new();

    public double  OverallScore  => ScoreCalculator.Calculate(Results);
    public string  Grade         => ScoreCalculator.GetGrade(OverallScore);
    public long    TotalDurationMs { get; set; }

    public List<Finding> AllFindings =>
        Results.Values.SelectMany(r => r.Findings).OrderByDescending(f => f.Level).ToList();
}
```

### ScanMode enum
```csharp
namespace LaptopForensics.Core.Models;

[Flags]
public enum ScanMode
{
    Quick  = 1,   // Hardware + Security + Users
    Full   = 2,   // All 10 modules
    Single = 4    // One specific module via --module flag
}
```

---

## 6. DESIGN PATTERNS (Where & How)

### Strategy Pattern
```
IScanModule implemented by all 10 scanners.
ScanOrchestrator takes IEnumerable<IScanModule> via DI.
Adding new scanner = implement interface + register in DI.
ScanOrchestrator.cs never changes.
```

### Factory Pattern
```
ScannerFactory.Create(ScanMode mode, IEnumerable<IScanModule> all)
  → returns filtered list based on mode
  → Quick mode: HardwareScanner, SecurityScanner, UserScanner only
  → Full mode: all 10
  → Single mode: one matching ModuleName
```

### Repository Pattern
```
IScanRepository → ScanRepository (SQLite implementation)
Core layer only knows IScanRepository.
Data layer provides concrete SQLite implementation.
In Phase 2: add RemoteScanRepository without touching Core.
```

### Builder Pattern
```
ScanOrchestrator builds ScanReport incrementally:
  1. Create empty ScanReport
  2. For each IScanModule: execute → add ModuleResult to report
  3. Set TotalDurationMs
  4. Pass to exporters
Report is NEVER constructed in one shot.
```

### Decorator Pattern
```
TimedScanModule wraps any IScanModule:
  - Records start time
  - Calls inner.ExecuteAsync()
  - Records end time
  - Overwrites DurationMs in result
  - Notifies IScanProgressObserver
ScanOrchestrator always works with TimedScanModule wrappers.
```

### Observer Pattern
```
IScanProgressObserver notified by TimedScanModule.
ConsoleUI implements IScanProgressObserver.
Updates Spectre.Console progress bar on each callback.
Decouples scanning logic from UI updates completely.
```

### Options Pattern
```
AppSettings loaded from appsettings.json via IOptions<AppSettings>.
Injected wherever needed.
Never use hardcoded paths or magic numbers — always via AppSettings.
```

---

## 7. TECH STACK & NUGET PACKAGES

### LaptopForensics.Console.csproj
```xml
<PackageReference Include="Spectre.Console" Version="0.49.1" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="8.0.0" />
<PackageReference Include="Serilog" Version="3.1.1" />
<PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
<PackageReference Include="Serilog.Sinks.Console" Version="4.1.0" />
```

### LaptopForensics.Core.csproj
```xml
<PackageReference Include="System.Management" Version="8.0.0" />
<PackageReference Include="Microsoft.PowerShell.SDK" Version="7.4.1" />
```

### LaptopForensics.Data.csproj
```xml
<PackageReference Include="System.Data.SQLite" Version="1.0.118" />
```

### LaptopForensics.Export.csproj
```xml
<PackageReference Include="System.Text.Json" Version="8.0.0" />
```

---

## 8. PUBLISH CONFIGURATION

### LaptopForensics.Console.csproj (runtime settings)
```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <TargetFramework>net8.0-windows</TargetFramework>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <SelfContained>true</SelfContained>
  <PublishSingleFile>true</PublishSingleFile>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
  <AssemblyName>laptop-forensics</AssemblyName>
</PropertyGroup>
```

---

## 9. ERROR HANDLING RULES (Never break these)

```
RULE 1: Every WMI call wrapped in try-catch ManagementException
        → Log warning, return empty/default data, NEVER throw

RULE 2: Every Registry read uses TryGetValue pattern
        → Missing key = return null, NEVER throw

RULE 3: Every PowerShell call has timeout (default 10 sec)
        → Timeout = log warning, return empty string

RULE 4: Every file/directory access catches UnauthorizedAccessException
        → Log warning, skip that path, continue

RULE 5: Each IScanModule has CancellationToken with 30-second timeout
        → Timeout = ModuleResult with Success=false, ErrorMessage="Timeout"

RULE 6: Stack traces NEVER shown to user
        → User sees: "⚠️ Hardware module incomplete: WMI unavailable"
        → Full error goes to Serilog log file only

RULE 7: No module failure stops other modules
        → ScanOrchestrator catches per-module exceptions
        → Scan continues with remaining modules
```

---

## 10. CODING STANDARDS

```
Naming:
  Classes       → PascalCase     (HardwareScanner)
  Interfaces    → IPascalCase    (IScanModule)
  Methods       → PascalCase     (ExecuteAsync)
  Properties    → PascalCase     (ModuleName)
  Private fields→ _camelCase     (_wmiService)
  Constants     → UPPER_SNAKE    (MAX_TIMEOUT_SECONDS)

File structure:
  One class per file
  File name = class name
  Namespace = LaptopForensics.[Layer].[Subfolder]

Async:
  All I/O operations must be async
  Method names end in Async
  Always pass CancellationToken

Null safety:
  Enable nullable reference types in all .csproj
  Use required keyword on non-nullable properties
  Use ? for nullable types explicitly

Comments:
  No commented-out code
  XML doc comments on all public interfaces and methods
  Inline comments only for non-obvious logic
```

---

## 11. SQLITE SCHEMA

```sql
-- Table: ScanHistory
CREATE TABLE IF NOT EXISTS ScanHistory (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    ScanId          TEXT    NOT NULL UNIQUE,
    ScanTimestamp   TEXT    NOT NULL,
    Hostname        TEXT    NOT NULL,
    OsVersion       TEXT,
    ScanMode        TEXT    NOT NULL,
    OverallScore    REAL,
    Grade           TEXT,
    ReportJson      TEXT,              -- Full ScanReport serialized as JSON
    TotalDurationMs INTEGER,
    CreatedAt       TEXT    DEFAULT (datetime('now'))
);

-- Index for history queries
CREATE INDEX IF NOT EXISTS idx_scan_timestamp ON ScanHistory(ScanTimestamp DESC);
CREATE INDEX IF NOT EXISTS idx_hostname ON ScanHistory(Hostname);
```

Default DB path: `C:\LaptopForensics\Data\scans.db`
Configurable via `appsettings.json → Storage.DatabasePath`

---

## 12. APPSETTINGS.JSON STRUCTURE

```json
{
  "App": {
    "Version": "1.0.0",
    "DefaultScanMode": "Full"
  },
  "Output": {
    "ReportDirectory": "C:\\LaptopForensics\\Reports\\",
    "DefaultFormats": ["console"],
    "HtmlEmbedCharts": true
  },
  "Storage": {
    "DatabasePath": "C:\\LaptopForensics\\Data\\scans.db",
    "KeepHistoryDays": 90
  },
  "Logging": {
    "LogDirectory": "C:\\LaptopForensics\\Logs\\",
    "MinimumLevel": "Warning"
  },
  "Scan": {
    "ModuleTimeoutSeconds": 30,
    "WatchIntervalMinutes": 30,
    "MaxConcurrentModules": 3
  },
  "Security": {
    "EncryptDatabase": false,
    "ReportAclUserOnly": true
  }
}
```

---

## 13. SCORING ALGORITHM

```csharp
// ScoreCalculator.cs
public static double Calculate(Dictionary<string, ModuleResult> results)
{
    var weights = new Dictionary<string, double>
    {
        { "HardwareScanner",    0.15 },
        { "SecurityScanner",    0.25 },
        { "UserScanner",        0.10 },
        { "NetworkScanner",     0.15 },
        { "SoftwareScanner",    0.10 },
        { "LicenseAuditor",     0.10 },
        { "StartupScanner",     0.05 },
        { "BrowserScanner",     0.05 },
        { "PerformanceScanner", 0.03 },
        { "ThreatDetector",     0.02 },
    };
    // Weighted average of available module scores
}

public static string GetGrade(double score) => score switch
{
    >= 90 => "EXCELLENT",
    >= 75 => "GOOD",
    >= 60 => "FAIR",
    _     => "POOR"
};
```

---

## 14. CLI ARGUMENT SPEC

```
laptop-forensics.exe [options]

Options:
  (no args)               Full scan, console output only
  --quick                 Hardware + Security + Users only
  --module <name>         Single module: hardware | users | network |
                          software | licenses | security | startup |
                          browser | performance | threats
  --export <format>       json | html | csv | all
  --output <path>         Output directory (default from appsettings)
  --silent                No console output, file export only
  --watch                 Repeat scan every --interval minutes
  --interval <minutes>    Used with --watch (default: 30)
  --history               Show past scans from SQLite
  --history-limit <n>     How many past scans to show (default: 10)
  --help                  Show this help
  --version               Show version
```

---

## 15. PHASE 2 READINESS NOTES

```
These decisions in Phase 1 make Phase 2 easy:

1. IScanRepository has a second impl in Phase 2: RemoteScanRepository
   → Just register different impl in DI. No core changes.

2. IScanProgressObserver has a second impl: WebSocketProgressObserver
   → Push real-time updates to dashboard. No scanner changes.

3. appsettings.json gets agent section:
   "Agent": { "Enabled": true, "ServerUrl": "https://..." }
   → ScanOrchestrator checks this flag, picks right repository.

4. All 10 IScanModule implementations stay 100% unchanged in Phase 2.
   Phase 2 only changes: storage destination + UI layer.
```

---
*End of ARCHITECTURE.md*
