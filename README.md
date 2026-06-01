# Laptop Forensics System

The **Laptop Forensics System** is a fast, lightweight, and completely local system analysis and forensics tool for Windows machines. It uses WMI, PowerShell, and the Registry to gather deep system insights without requiring an internet connection.

## Features

- **Hardware Analysis:** Analyzes CPU, RAM, Disk Drives, GPU, Battery, and BIOS information.
- **User Auditing:** Lists all local accounts, active sessions, and checks for potential active directory anomalies.
- **Network Scanning:** Checks active network connections and identifies risky open ports (e.g. Remote Desktop, File Sharing).
- **Software Discovery:** Detects installed software, highlights bloatware, and identifies potentially unwanted programs.
- **License Auditing:** Scans for Windows and Office activation states to ensure compliance.
- **Security Posture:** Checks Firewall status, Windows Defender status, BitLocker encryption, UAC settings, and Secure Boot.
- **Startup Analysis:** Finds autorun items, scheduled tasks, and rogue services starting with the system.
- **Browser Forensics:** Identifies installed browser extensions across Chrome, Edge, and Firefox.
- **Performance Profiling:** Checks real-time memory pressure and CPU bottlenecks.
- **Threat Detection:** Built-in blacklists for suspicious processes, bad hosts file entries, and malware indicators.

## Export & Reporting

The tool generates forensic reports locally on the machine in the following formats:
- `JSON`: Complete raw data dump containing all collected information.
- `HTML`: Beautiful, interactive summary dashboard.
- `CSV`: Easy-to-parse tabular data of installed software and findings.
- `SQLite`: Automatically logs scan history locally for audit tracking.

## Getting Started

### Building the Project

This project uses .NET 8. You can build a self-contained executable that can run on any Windows machine (even without .NET installed).

1. Open PowerShell in the root directory.
2. Run the build script:
   ```powershell
   .\build.ps1
   ```
3. The standalone executable will be created at: `publish\laptop-forensics.exe`.

### Running the Tool

You can run the compiled executable from the command line. Administrative privileges are required for some of the deep forensics (like BitLocker checks or certain WMI queries). The tool will prompt for UAC elevation automatically if it needs it.

**Quick Scan (Faster, skips deep file scans):**
```powershell
.\laptop-forensics.exe --quick
```

**Full Scan with All Reports:**
```powershell
.\laptop-forensics.exe --full --export all
```

**View Previous Scans (History):**
```powershell
.\laptop-forensics.exe --history
```

**Silent Mode (No UI, great for scripts):**
```powershell
.\laptop-forensics.exe --quick --silent --export json
```

## Supported Exports
You can specify one or more exporters with the `--export` flag:
- `json`
- `csv`
- `html`
- `console` (Default)
- `all`

Example: `--export json,html`

## Architecture

This project is built using C# and .NET 8, following Domain-Driven Design (DDD) principles:
- **Core:** Contains models, interfaces, and core business logic (Scanners).
- **Data:** SQLite repository for scan history.
- **Export:** Handlers for generating JSON, CSV, and HTML reports.
- **Console:** Spectre.Console UI and CLI entry point.
