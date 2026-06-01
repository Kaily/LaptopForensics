<div align="center">

# 🛡️ SentinelAI Windows Laptop Forensics
**A Forensic-Grade, Ultra-Fast Incident Response & Security Scanning Tool for Windows**

[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-blue.svg)]()
[![License](https://img.shields.io/badge/License-MIT-green.svg)]()
[![Status](https://img.shields.io/badge/Status-Production--Ready-success.svg)]()

</div>

---

**SentinelAI Laptop Forensics** is a powerful, locally-executed C# (.NET 8) forensic analysis and security auditing tool designed for Windows. It completely bypasses generic, high-level scanning by performing deep inspections into WMI, Registry, Event Logs, and Windows APIs to uncover process spoofing, rogue browser extensions, insecure configurations, and persistence mechanisms. 

It generates beautiful console dashboards, comprehensive JSON forensic dumps for SIEM integration, and provides **exact remediation commands** for discovered threats.

---

## 🔥 Key Features (Forensic Depth)

SentinelAI goes beyond simple generic alerts. Every critical finding includes a granular **Evidence Dictionary** consisting of exact PIDs, File Paths, expected behaviors, and instant remediation commands.

- 🦠 **Process & Threat Analysis**: Scans running processes via WMI for known malware (e.g., `mimikatz`, `xmrig`) and detects process spoofing (e.g., `svchost.exe` running from `C:\Users\...\AppData`). Outputs exact `taskkill` remediation.
- 🌐 **Deep Browser Forensics**: Directly parses `manifest.json` files for installed Chrome, Edge, and Firefox extensions. Calculates a risk score based on requested permissions (e.g., `nativeMessaging`, `cookies`) and provides physical extension paths for manual deletion.
- 🚀 **Persistence & Startup Checks**: Audits Autorun registry keys, identifying rogue executables that launch on boot. Returns exact Registry Keys (`HKLM\Software\Microsoft\Windows\CurrentVersion\Run`) and Image Paths.
- 📡 **Network Connection Mapping**: Identifies open ports and directly resolves the PID of the process holding risky ports (e.g., SMB 445, RDP 3389).
- 🛡️ **Hardening & Security Auditing**: Verifies BitLocker status, Windows Defender definitions, UAC configuration, and Firewall profiles. Provides exact `wmic` and `netsh` commands to patch vulnerabilities immediately.
- 👥 **User Account Auditing**: Detects stale local accounts, enabled built-in Administrators, missing password expiry policies, and potential brute-force targets.

## 📊 Exporting & SIEM Integration

Reports are generated locally and securely. No data is sent over the internet.
*   **Console (Default)**: Interactive, beautifully rendered terminal dashboard using Spectre.Console.
*   **JSON Exporter**: Serializes the complete scan, including deep nested `Evidence` maps, for seamless ingestion into SIEM platforms (e.g., Splunk, Sentinel, Elastic).
*   **HTML**: An interactive web-based report (coming soon/optional integration).
*   **SQLite History**: Tracks previous scans to measure security posture over time.

---

## 🚀 Getting Started

### Prerequisites
*   Windows 10 / 11 or Windows Server.
*   **To compile from source:** .NET 8 SDK.
*   **To run:** The compiled executable is self-contained and requires *no dependencies* on the target machine.

### Building the Project
Clone the repository and run the build command to generate a self-contained executable.

```powershell
git clone https://github.com/yourusername/SentinelAI-Laptop-Forensics.git
cd SentinelAI-Laptop-Forensics

# Build a standalone executable
dotnet publish src\LaptopForensics.Console\LaptopForensics.Console.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The resulting executable will be placed in the `publish` directory (or standard bin output), ready to be dropped onto any Windows host.

### Running the Scanner

> **⚠️ NOTE:** Run the executable from an **Administrator PowerShell** to ensure deep WMI and Registry read access.

```powershell
# Run the default fast scan with a beautiful console output
.\laptop-forensics.exe --mode quick

# Run a full scan and export the results to JSON in the current directory
.\laptop-forensics.exe --mode full --export json
```

---

## 🛠️ Architecture

Built using modern **Domain-Driven Design (DDD)** in C#:
- `LaptopForensics.Core`: The central intelligence. Contains all data models (`Finding`, `Evidence`), WMI query wrappers, and the Strategy Pattern-based `IScanModule` classes (e.g., `BrowserScanner`, `ThreatDetector`).
- `LaptopForensics.Data`: Entity Framework / SQLite implementation for local historical reporting.
- `LaptopForensics.Export`: Exporter implementations (`JsonExporter`, `ConsoleReporter`).
- `LaptopForensics.Console`: The entry point utilizing `System.CommandLine` and `Spectre.Console` for rich UI.

---

## 🛡️ Sample Output

When SentinelAI detects an anomaly, it doesn't just say "Malware Found". It provides exact forensic evidence:

```yaml
- Critical: svchost (Confidence: High)
  Malware or Process Spoofing Detected.
  Evidence:
    - PID: 9432
    - Path: C:\Users\Admin\AppData\Local\Temp\svchost.exe
    - Reason: Legitimate Windows process name running from a suspicious directory.
    - Expected: C:\Windows\System32\svchost.exe
    - Action: taskkill /F /PID 9432

- Warning: Insecure Browser Extension (Confidence: High)
  Found 'Free VPN Proxy' requesting risky permissions.
  Evidence:
    - Extension ID: mcbkbpnllk...
    - Permissions: <all_urls>, tabs, webRequest
    - Risk Score: 8.5
    - Path: C:\Users\Admin\AppData\Local\Google\Chrome\User Data\Default\Extensions\...
```

---

## 🤝 Contributing

Contributions are welcome! If you want to add new scanners (e.g., full Event Log forensics, YARA rule integration, or Memory dumping), please open an issue or submit a Pull Request. 

1. Fork the Project
2. Create your Feature Branch (`git checkout -b feature/AmazingFeature`)
3. Commit your Changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the Branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📄 License

Distributed under the MIT License. See `LICENSE` for more information.

---
*Disclaimer: This tool is intended for authorized auditing, forensic analysis, and incident response on systems you own or have explicit permission to scan.*
