using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using LaptopForensics.Core.Helpers;
using LaptopForensics.Core.Interfaces;
using LaptopForensics.Core.Models;
using Microsoft.Extensions.Logging;

namespace LaptopForensics.Core.Scanners;

public class SecurityScanner : IScanModule
{
    private readonly IWmiService _wmi;
    private readonly IPowerShellService _ps;
    private readonly IRegistryService _registry;
    private readonly ILogger<SecurityScanner> _logger;

    public string ModuleName => "SecurityScanner";
    public string ModuleIcon => "*";
    public int EstimatedSeconds => 12;
    public ScanMode ApplicableModes => ScanMode.Full | ScanMode.Quick;

    public SecurityScanner(IWmiService wmi, IPowerShellService ps, IRegistryService registry, ILogger<SecurityScanner> logger)
    {
        _wmi = wmi;
        _ps = ps;
        _registry = registry;
        _logger = logger;
    }

    public async Task<ModuleResult> ExecuteAsync(CancellationToken ct)
    {
        var resultData = new SecurityStatus();
        var findings = new List<Finding>();
        double score = 100;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // 1. Antivirus
            try
            {
                var avs = _wmi.Query("AntiVirusProduct", null, @"root\SecurityCenter2").ToList();
                if (avs.Any())
                {
                    var av = avs.First();
                    resultData.Antivirus.ProductName = av.ContainsKey("displayName") ? av["displayName"]?.ToString() ?? "" : "";
                    
                    if (av.ContainsKey("productState") && av["productState"] != null)
                    {
                        if (uint.TryParse(av["productState"]?.ToString(), out uint state))
                        {
                            var stateHex = state.ToString("X6");
                            if (stateHex.Length >= 4)
                            {
                                // typical state e.g., 0x39700 (disabled), 0x39710 (enabled)
                                // bit logic via hex string isn't standard, standard is bitwise:
                                // state & 0x1000 = enabled, state & 0x0010 = upToDate
                                resultData.Antivirus.IsEnabled = (state & 0x1000) != 0;
                                resultData.Antivirus.IsUpToDate = (state & 0x0010) == 0;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query AntiVirusProduct from WMI");
            }

            // 2. Windows Defender
            try
            {
                var defOut = await _ps.RunAndGetObjectsAsync("Get-MpComputerStatus", 10);
                if (defOut != null && defOut.Any())
                {
                    var def = defOut.First();
                    if (def.Properties["AMServiceEnabled"]?.Value is bool amEnabled) resultData.Defender.AntivirusEnabled = amEnabled;
                    if (def.Properties["AntispywareEnabled"]?.Value is bool asEnabled) resultData.Defender.AntivirusEnabled = resultData.Defender.AntivirusEnabled || asEnabled; // roughly combined
                    if (def.Properties["RealTimeProtectionEnabled"]?.Value is bool rtEnabled) resultData.Defender.RealTimeEnabled = rtEnabled;
                    
                    var sigUpdate = def.Properties["AntivirusSignatureLastUpdated"]?.Value;
                    if (sigUpdate is DateTime dtSig) resultData.Defender.SignatureDate = dtSig;

                    var lastScan = def.Properties["QuickScanStartTime"]?.Value;
                    if (lastScan is DateTime dtScan) resultData.Defender.LastScanDate = dtScan;

                    resultData.Defender.SignatureVersion = def.Properties["AntivirusSignatureVersion"]?.Value?.ToString() ?? "";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query Defender status");
            }

            // 3. Firewall
            try
            {
                var fwOut = await _ps.RunAndGetObjectsAsync("Get-NetFirewallProfile | Select-Object Name, Enabled", 10);
                if (fwOut != null)
                {
                    foreach (var fw in fwOut)
                    {
                        var name = fw.Properties["Name"]?.Value?.ToString();
                        var enabled = fw.Properties["Enabled"]?.Value is int e && e == 1 || fw.Properties["Enabled"]?.Value is bool b && b;
                        
                        if (name == "Domain") resultData.Firewall.DomainEnabled = enabled;
                        else if (name == "Private") resultData.Firewall.PrivateEnabled = enabled;
                        else if (name == "Public") resultData.Firewall.PublicEnabled = enabled;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query Firewall status");
            }

            // 4. BitLocker
            try
            {
                var bdeOut = await _ps.RunAsync("manage-bde -status", 15);
                if (string.IsNullOrEmpty(bdeOut) || bdeOut.Contains("ERROR"))
                {
                    var blVol = await _ps.RunAndGetObjectsAsync("Get-BitLockerVolume", 15);
                    if (blVol != null)
                    {
                        foreach (var vol in blVol)
                        {
                            var status = new DriveEncryptionStatus
                            {
                                DriveLetter = vol.Properties["MountPoint"]?.Value?.ToString() ?? "",
                                IsEncrypted = vol.Properties["VolumeStatus"]?.Value?.ToString() == "FullyEncrypted",
                                Method = vol.Properties["EncryptionMethod"]?.Value?.ToString() ?? ""
                            };
                            resultData.BitLocker.Drives.Add(status);
                        }
                    }
                }
                else
                {
                    var lines = bdeOut.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    DriveEncryptionStatus? currentDrive = null;
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("Volume "))
                        {
                            if (currentDrive != null) resultData.BitLocker.Drives.Add(currentDrive);
                            currentDrive = new DriveEncryptionStatus { DriveLetter = line.Replace("Volume ", "").Trim() };
                        }
                        else if (currentDrive != null)
                        {
                            if (line.Contains("Protection Status:"))
                                currentDrive.IsEncrypted = line.Contains("Protection On");
                            if (line.Contains("Encryption Method:"))
                                currentDrive.Method = line.Split(':').Last().Trim();
                        }
                    }
                    if (currentDrive != null) resultData.BitLocker.Drives.Add(currentDrive);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query BitLocker status");
            }

            // 5. UAC
            try
            {
                var uacKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
                var enableLua = _registry.GetValue(RegistryHive.LocalMachine, uacKey, "EnableLUA");
                if (enableLua is int eLua)
                {
                    resultData.Uac.IsEnabled = eLua == 1;
                }
                
                var consent = _registry.GetValue(RegistryHive.LocalMachine, uacKey, "ConsentPromptBehaviorAdmin");
                if (consent is int c)
                {
                    resultData.Uac.Level = c;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query UAC status");
            }

            // 6. SecureBoot
            try
            {
                var sbOut = await _ps.RunAsync("Confirm-SecureBootUEFI", 10);
                if (sbOut.Contains("PlatformNotSupportedException") || sbOut.Contains("CmdletInvocationException"))
                {
                    resultData.SecureBoot.IsBiosMode = true;
                    resultData.SecureBoot.IsEnabled = null;
                }
                else if (bool.TryParse(sbOut.Trim(), out bool isEnabled))
                {
                    resultData.SecureBoot.IsEnabled = isEnabled;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query SecureBoot");
            }

            // 7. Windows Updates
            try
            {
                var hfOut = await _ps.RunAndGetObjectsAsync("Get-HotFix | Sort-Object InstalledOn -Descending | Select-Object -First 10", 15);
                if (hfOut != null)
                {
                    foreach (var hf in hfOut)
                    {
                        var id = hf.Properties["HotFixID"]?.Value?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(id)) resultData.WindowsUpdates.RecentHotfixes.Add(id);

                        if (resultData.WindowsUpdates.LastUpdateDate == null)
                        {
                            if (hf.Properties["InstalledOn"]?.Value is DateTime dt)
                            {
                                resultData.WindowsUpdates.LastUpdateDate = dt;
                            }
                        }
                    }
                }
                
                // Pending updates count check (best effort using PS script to not block forever)
                var pendingOut = await _ps.RunAsync("$updateSession = New-Object -ComObject Microsoft.Update.Session; $updateSearcher = $updateSession.CreateUpdateSearcher(); $searchResult = $updateSearcher.Search('IsInstalled=0'); $searchResult.Updates.Count", 20);
                if (int.TryParse(pendingOut.Trim(), out int pendingCount))
                {
                    resultData.WindowsUpdates.PendingUpdates = pendingCount;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query Windows Updates");
            }

            // 8. Event log errors last 7 days
            try
            {
                int errorCount = 0;
                var cutoff = DateTime.Now.AddDays(-7);
                
                // We'll use a very fast approach, only counting, no loading message
                var logsToSearch = new[] { "System", "Application" };
                foreach (var logName in logsToSearch)
                {
                    using var eventLog = new EventLog(logName);
                    // Iterate backwards for speed
                    for (int i = eventLog.Entries.Count - 1; i >= 0; i--)
                    {
                        if (ct.IsCancellationRequested) break;
                        
                        var entry = eventLog.Entries[i];
                        if (entry.TimeGenerated < cutoff)
                            break; // Because they are typically sorted by time

                        if (entry.EntryType == EventLogEntryType.Error)
                        {
                            errorCount++;
                        }
                    }
                }
                resultData.RecentErrors.Add($"{errorCount} errors in last 7 days");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query Event Logs");
            }

            // 9. Score and findings
            if (!resultData.Defender.RealTimeEnabled && !resultData.Antivirus.IsEnabled)
            {
                score -= 30;
                var evidence = new Dictionary<string, string>
                {
                    { "Current State", "Disabled" },
                    { "Expected", "Enabled" },
                    { "Action", "Set-MpPreference -DisableRealtimeMonitoring $false" }
                };
                findings.Add(new Finding { Level = Severity.Critical, Title = "Real-time antivirus disabled", Description = "No active real-time protection was detected.", Recommendation = "Enable Windows Defender or a third-party antivirus.", Confidence = ConfidenceLevel.High, Evidence = evidence });
            }

            if (!resultData.Firewall.AllEnabled)
            {
                score -= 25;
                var evidence = new Dictionary<string, string>
                {
                    { "Current State", "One or more profiles disabled" },
                    { "Expected", "Domain, Private, and Public profiles Enabled" },
                    { "Action", "Set-NetFirewallProfile -Profile Domain,Public,Private -Enabled True" }
                };
                findings.Add(new Finding { Level = Severity.Critical, Title = "Firewall disabled", Description = "One or more firewall profiles are disabled.", Recommendation = "Enable all Windows Firewall profiles.", Confidence = ConfidenceLevel.High, Evidence = evidence });
            }

            var sigDate = resultData.Defender.SignatureDate ?? resultData.Antivirus.LastUpdateDate;
            if (sigDate.HasValue && sigDate.Value < DateTime.Now.AddDays(-7))
            {
                score -= 20;
                var evidence = new Dictionary<string, string>
                {
                    { "Current State", $"Last updated on {sigDate.Value:yyyy-MM-dd}" },
                    { "Expected", "Updated within last 7 days" },
                    { "Action", "Update-MpSignature" }
                };
                findings.Add(new Finding { Level = Severity.Warning, Title = "Antivirus definitions out of date", Description = "Definitions are older than 7 days.", Recommendation = "Update antivirus definitions.", Confidence = ConfidenceLevel.High, Evidence = evidence });
            }

            if (!resultData.Uac.IsEnabled)
            {
                score -= 15;
                var evidence = new Dictionary<string, string>
                {
                    { "Current State", "Disabled (EnableLUA = 0)" },
                    { "Expected", "Enabled (EnableLUA = 1)" },
                    { "Fix", @"reg add HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System /v EnableLUA /t REG_DWORD /d 1 /f" }
                };
                findings.Add(new Finding { Level = Severity.Critical, Title = "UAC completely disabled", Description = "User Account Control is disabled.", Recommendation = "Enable UAC in Windows settings.", Confidence = ConfidenceLevel.High, Evidence = evidence });
            }

            var systemDrive = resultData.BitLocker.Drives.FirstOrDefault(d => d.DriveLetter.StartsWith("C", StringComparison.OrdinalIgnoreCase));
            if (systemDrive != null && !systemDrive.IsEncrypted)
            {
                score -= 15;
                var evidence = new Dictionary<string, string>
                {
                    { "Current State", $"Drive {systemDrive.DriveLetter} Unencrypted" },
                    { "Expected", "FullyEncrypted" },
                    { "Fix", $"manage-bde -on {systemDrive.DriveLetter}" }
                };
                findings.Add(new Finding { Level = Severity.Warning, Title = "System drive not encrypted", Description = "BitLocker is not enabled on the system drive.", Recommendation = "Enable BitLocker on drive C:.", Confidence = ConfidenceLevel.High, Evidence = evidence });
            }

            if (resultData.WindowsUpdates.PendingUpdates > 10)
            {
                score -= 10;
                var evidence = new Dictionary<string, string>
                {
                    { "Current State", $"{resultData.WindowsUpdates.PendingUpdates} updates pending" },
                    { "Expected", "0 updates pending" },
                    { "Fix", "Settings -> Windows Update -> Check for updates" }
                };
                findings.Add(new Finding { Level = Severity.Warning, Title = "Pending Windows Updates", Description = $"{resultData.WindowsUpdates.PendingUpdates} updates are pending.", Recommendation = "Install pending Windows updates.", Confidence = ConfidenceLevel.Medium, Evidence = evidence });
            }

            if (resultData.SecureBoot.IsEnabled.HasValue && !resultData.SecureBoot.IsEnabled.Value)
            {
                score -= 10;
                var evidence = new Dictionary<string, string>
                {
                    { "Current State", "Disabled" },
                    { "Expected", "Enabled" },
                    { "Fix", "Reboot to BIOS/UEFI and enable SecureBoot." }
                };
                findings.Add(new Finding { Level = Severity.Warning, Title = "SecureBoot disabled", Description = "SecureBoot is supported but disabled.", Recommendation = "Enable SecureBoot in BIOS/UEFI settings.", Confidence = ConfidenceLevel.High, Evidence = evidence });
            }

            if (resultData.Defender.LastScanDate.HasValue && resultData.Defender.LastScanDate.Value < DateTime.Now.AddDays(-7))
            {
                score -= 5;
                var evidence = new Dictionary<string, string>
                {
                    { "Current State", $"Last scan on {resultData.Defender.LastScanDate.Value:yyyy-MM-dd}" },
                    { "Expected", "Scanned within last 7 days" },
                    { "Action", "Start-MpScan -ScanType QuickScan" }
                };
                findings.Add(new Finding { Level = Severity.Info, Title = "No recent antivirus scan", Description = "Last scan was more than 7 days ago.", Recommendation = "Run a quick scan.", Confidence = ConfidenceLevel.Medium, Evidence = evidence });
            }

            score = Math.Max(0, score);
            stopwatch.Stop();

            return new ModuleResult
            {
                ModuleName = ModuleName,
                Success = true,
                Score = score,
                Grade = ScoreCalculator.GetGrade(score),
                Findings = findings,
                Data = resultData,
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SecurityScanner failed entirely.");
            stopwatch.Stop();
            return new ModuleResult
            {
                ModuleName = ModuleName,
                Success = false,
                Score = 0,
                Grade = "POOR",
                Findings = new List<Finding>(),
                Data = new SecurityStatus(),
                DurationMs = stopwatch.ElapsedMilliseconds,
                ErrorMessage = ex.Message
            };
        }
    }
}
