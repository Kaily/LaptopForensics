<div align="center">

# 🛡️ Laptop Forensics System

**A forensic-grade security and health auditing tool for Windows laptops and workstations.**

[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-blue.svg)]()
[![License](https://img.shields.io/badge/License-MIT-green.svg)]()
[![Status](https://img.shields.io/badge/Status-v1.0.0-success.svg)]()

</div>

---

Laptop Forensics System is a locally-executed C# (.NET 8) command-line tool that performs a deep audit of a Windows machine. It inspects WMI, the registry, and Windows APIs across ten scan modules — covering hardware inventory, installed software, licensing, network exposure, security posture, startup persistence, browser extensions, user accounts, performance, and active threats.

Each finding ships with concrete **evidence** (PIDs, file paths, registry keys, permissions) and, where relevant, an exact remediation command. Results render as a rich terminal dashboard and can be exported to JSON, CSV, or HTML. Every scan is stored in a local SQLite history so you can track posture over time.

> **All analysis runs locally. No data leaves the machine.**

---

## ✨ Scan Modules

| Module | Icon | What it checks | Quick scan |
|--------|:----:|----------------|:----------:|
| `HardwareScanner` | 🖥️ | CPU, memory, disk, and system hardware inventory | ✅ |
| `UserScanner` | 👤 | Local accounts, stale users, built-in Administrator, password policy | ✅ |
| `SoftwareScanner` | 📦 | Installed applications and versions | ✅ |
| `SecurityScanner` | 🔒 | BitLocker, Windows Defender, UAC, and firewall configuration | ✅ |
| `StartupScanner` | 🚀 | Autorun registry keys and boot-time persistence | ✅ |
| `BrowserScanner` | 🌐 | Chrome/Edge/Firefox extensions and permission risk scoring | ✅ |
| `PerformanceScanner` | ⚡ | CPU, memory, and disk utilization metrics | ✅ |
| `ThreatDetector` | 🛡️ | Known-malware process names and process spoofing | ✅ |
| `NetworkScanner` | 🌐 | Open ports and the processes holding risky ports (SMB, RDP) | Full only |
| `LicenseAuditor` | 🔑 | Windows and software licensing status | Full only |

A **Quick** scan runs the eight fast modules above. A **Full** scan runs all ten. You can also run any single module by name.

---

## 🚀 Getting Started

### Prerequisites

- **To run:** Windows 10/11 or Windows Server (x64). The published executable is self-contained — no .NET runtime required on the target machine.
- **To build:** [.NET 8 SDK](https://dotnet.microsoft.com/download).
- **Recommended:** run from an **Administrator** terminal. Without elevation, some WMI and registry reads return incomplete data (the tool warns you and continues).

### Build

Clone the repository and run the build script from the repo root:

```powershell
git clone https://github.com/<your-org>/laptop-forensics-system.git
cd laptop-forensics-system

# Produces a self-contained single-file executable in .\publish
.\build.ps1

# Build and immediately run a quick scan
.\build.ps1 -RunAfterBuild
```

Or invoke `dotnet` directly:

```powershell
dotnet publish src\LaptopForensics.Console\LaptopForensics.Console.csproj `
    -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

The result is `publish\laptop-forensics.exe`, ready to drop onto any Windows x64 host.

### Run

```powershell
# Full scan with the interactive console dashboard (default)
.\laptop-forensics.exe

# Fast scan of the eight core modules
.\laptop-forensics.exe --quick

# Run a single module
.\laptop-forensics.exe --module ThreatDetector

# Full scan, export JSON + HTML into a chosen folder
.\laptop-forensics.exe --export "json,html" --output C:\Audits

# Unattended: no console UI, export everything
.\laptop-forensics.exe --silent --export all

# Continuous monitoring every 30 minutes
.\laptop-forensics.exe --watch --interval 30 --export json

# Review past scans
.\laptop-forensics.exe --history
```

### Command-line options

| Flag | Description |
|------|-------------|
| `--quick` | Run the quick scan (fast modules only) instead of a full scan |
| `--module <name>` | Run a single module (e.g. `SecurityScanner`, `BrowserScanner`) |
| `--export <formats>` | Export format(s): `console`, `json`, `csv`, `html`, or `all` (comma-separable) |
| `--output <path>` | Directory for exported reports (default: `.\Reports`) |
| `--silent` | Suppress the console UI (for scheduled/unattended runs) |
| `--watch` | Loop the scan on an interval until interrupted |
| `--interval <minutes>` | Delay between watch-mode runs (default: 60) |
| `--history` | Print previously stored scan runs and exit |
| `--version` | Print the version and exit |
| `--help` | Show usage and exit |

### Exit codes

| Code | Meaning |
|:----:|---------|
| `0` | Scan completed with no findings |
| `2` | Scan completed but findings were reported |
| `1` | The scan crashed (see logs) |

These make the tool easy to wire into CI pipelines and scheduled tasks.

---

## 📤 Output & Reporting

- **Console** — an interactive terminal dashboard rendered with [Spectre.Console](https://spectreconsole.net/), including a live progress bar and per-module status.
- **JSON** — the complete scan with nested evidence maps, suitable for ingestion into SIEM platforms (Splunk, Sentinel, Elastic).
- **CSV** — flattened findings for spreadsheets and quick triage.
- **HTML** — a self-contained web report.
- **SQLite history** — every run is persisted locally so you can measure security posture over time (`--history`).

Default output paths, database location, log level, module timeouts, and watch interval are configurable in [`appsettings.json`](src/LaptopForensics.Console/appsettings.json).

---

## 🧩 Sample Findings

Findings carry structured evidence rather than a bare alert:

```yaml
- Critical: svchost (Confidence: High)
  Process spoofing detected.
  Evidence:
    - PID:      9432
    - Path:     C:\Users\Admin\AppData\Local\Temp\svchost.exe
    - Expected: C:\Windows\System32\svchost.exe
    - Reason:   Legitimate Windows process name running from a suspicious directory.
    - Action:   taskkill /F /PID 9432

- Warning: Insecure Browser Extension (Confidence: High)
  'Free VPN Proxy' requests high-risk permissions.
  Evidence:
    - Permissions: <all_urls>, tabs, webRequest
    - Risk Score:  8.5
    - Path:        C:\Users\Admin\AppData\Local\Google\Chrome\User Data\Default\Extensions\...
```

---

## 🏗️ Architecture

The solution follows a layered design with a pluggable scan-module pattern:

| Project | Responsibility |
|---------|----------------|
| `LaptopForensics.Console` | Entry point, CLI parsing, and the Spectre.Console UI |
| `LaptopForensics.Core` | Domain models, scan modules (`IScanModule`), orchestration, and Windows services (WMI, registry, PowerShell) |
| `LaptopForensics.Data` | Entity Framework + SQLite persistence for scan history |
| `LaptopForensics.Export` | `IExporter` implementations: console, JSON, CSV, HTML |
| `LaptopForensics.Tests` | Unit tests |

Each scanner implements `IScanModule` and is discovered via dependency injection, so new modules can be added without touching the orchestrator. A `ScannerFactory` selects modules by scan mode, and a `ScanOrchestrator` runs them, wrapping each in a `TimedScanModule` for progress reporting.

Additional design docs live in [`docs/`](docs/): [`ARCHITECTURE.md`](docs/ARCHITECTURE.md), [`MODULE_CONTRACTS.md`](docs/MODULE_CONTRACTS.md), and [`BUILD_STATUS.md`](docs/BUILD_STATUS.md).

---

## 🧪 Development

```powershell
# Restore and build the whole solution
dotnet build LaptopForensics.sln

# Run the test suite
dotnet test

# Run from source without publishing
dotnet run --project src\LaptopForensics.Console -- --quick
```

**Tech stack:** .NET 8, Spectre.Console, Serilog, Entity Framework (SQLite), Microsoft.Extensions.DependencyInjection.

---

## 🤝 Contributing

Contributions are welcome — new scan modules (event-log forensics, YARA rules, memory analysis) are a natural fit thanks to the `IScanModule` contract.

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/my-module`)
3. Commit your changes
4. Push and open a Pull Request

---

## 📄 License

Distributed under the MIT License. See `LICENSE` for details.

---

> **Disclaimer:** This tool is intended for authorized auditing, forensic analysis, and incident response on systems you own or have explicit permission to scan.
