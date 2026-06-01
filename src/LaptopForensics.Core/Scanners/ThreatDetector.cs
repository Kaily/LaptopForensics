using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using LaptopForensics.Core.Helpers;
using LaptopForensics.Core.Interfaces;
using LaptopForensics.Core.Models;
using Microsoft.Extensions.Logging;

namespace LaptopForensics.Core.Scanners;

public class ThreatDetector : IScanModule
{
    private readonly IRegistryService _registry;
    private readonly ILogger<ThreatDetector> _logger;

    public string ModuleName => "ThreatDetector";
    public string ModuleIcon => "🛡️";
    public int EstimatedSeconds => 5;
    public ScanMode ApplicableModes => ScanMode.Full | ScanMode.Quick;

    public ThreatDetector(IRegistryService registry, ILogger<ThreatDetector> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    public Task<ModuleResult> ExecuteAsync(CancellationToken ct)
    {
        var resultData = new ThreatScanResult();
        var globalFindings = new List<Finding>();
        double score = 100;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // 1. Process Blacklist
            var maliciousProcesses = new[] { "minerd", "xmrig", "nc", "ncat", "psexec", "mimikatz", "wannacry", "notpetya", "pwdump" };
            try
            {
                var processes = Process.GetProcesses();
                foreach (var process in processes)
                {
                    if (ct.IsCancellationRequested) break;
                    
                    try
                    {
                        var name = process.ProcessName.ToLowerInvariant();
                        if (maliciousProcesses.Any(m => name.Contains(m)))
                        {
                            resultData.Threats.Add(new ThreatFinding
                            {
                                Name = process.ProcessName,
                                Type = "Process",
                                Detail = $"Suspicious process found running with PID {process.Id}",
                                Severity = Severity.Critical
                            });
                        }
                    }
                    catch
                    {
                        // Ignore access denied on specific processes
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to scan processes for threats");
            }

            // 2. Hosts File
            try
            {
                var hostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
                if (File.Exists(hostsPath))
                {
                    var lines = File.ReadAllLines(hostsPath);
                    resultData.FilesScanned++;

                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                            continue;

                        // Check if it's localhost mapping
                        if (trimmed.StartsWith("127.0.0.1") || trimmed.StartsWith("::1"))
                        {
                            if (trimmed.Contains("localhost")) continue; // Normal
                            
                            // Check for known adblockers/telemetry blockers (often map to 127.0.0.1)
                            // We will flag if they map microsoft or google to loopback as it could be malicious or just a tracker blocker.
                        }

                        // Just flag any non-comment, non-empty, non-localhost line as potentially suspicious
                        resultData.HostsTampered = true;
                        resultData.SuspiciousHosts.Add(trimmed);
                        
                        resultData.Threats.Add(new ThreatFinding
                        {
                            Name = "Hosts File Entry",
                            Type = "File",
                            Detail = $"Non-standard hosts file entry: {trimmed}",
                            Severity = Severity.Warning
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse hosts file");
            }

            // 3. Suspicious Listening Ports
            try
            {
                var properties = IPGlobalProperties.GetIPGlobalProperties();
                var endpoints = properties.GetActiveTcpListeners();
                
                var suspiciousPorts = new Dictionary<int, (string Name, Severity Sev)>
                {
                    { 4444, ("Metasploit / Common Bind Shell", Severity.Critical) },
                    { 3389, ("RDP (Remote Desktop)", Severity.Warning) },
                    { 5900, ("VNC", Severity.Warning) },
                    { 135, ("RPC Bind", Severity.Info) },
                    { 445, ("SMB", Severity.Info) }
                };

                foreach (var ep in endpoints)
                {
                    if (suspiciousPorts.TryGetValue(ep.Port, out var info))
                    {
                        // We only want to flag if it's listening on all interfaces (0.0.0.0) or specific external interfaces
                        if (ep.Address.ToString() == "0.0.0.0" || ep.Address.ToString() == "::")
                        {
                            resultData.Threats.Add(new ThreatFinding
                            {
                                Name = $"Listening Port {ep.Port}",
                                Type = "Network",
                                Detail = $"Port {ep.Port} ({info.Name}) is listening on {ep.Address}",
                                Severity = info.Sev
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to scan listening ports");
            }

            // 4. Recently Modified Executables in System32 (Rootkit/tampering check)
            try
            {
                var sys32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
                var cutoff = DateTime.Now.AddDays(-7);
                var files = Directory.GetFiles(sys32, "*.exe").Concat(Directory.GetFiles(sys32, "*.dll")).ToList();
                int recentCount = 0;

                foreach (var file in files)
                {
                    if (ct.IsCancellationRequested) break;
                    var info = new FileInfo(file);
                    if (info.LastWriteTime > cutoff)
                    {
                        recentCount++;
                    }
                }

                if (recentCount > 5)
                {
                    resultData.Threats.Add(new ThreatFinding
                    {
                        Name = "System32 Modifications",
                        Type = "File",
                        Detail = $"Found {recentCount} executables/DLLs modified in the last 7 days in System32.",
                        Severity = Severity.Warning
                    });
                }
                resultData.FilesScanned += files.Count;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check recently modified executables in System32");
            }

            // 5. Calculate Score and populate Findings
            foreach (var threat in resultData.Threats)
            {
                if (threat.Severity == Severity.Critical)
                {
                    score -= 20;
                    globalFindings.Add(new Finding { Level = Severity.Critical, Title = threat.Name, Description = threat.Detail, Recommendation = "Immediate investigation required." });
                }
                else if (threat.Severity == Severity.Warning)
                {
                    score -= 10;
                    globalFindings.Add(new Finding { Level = Severity.Warning, Title = threat.Name, Description = threat.Detail, Recommendation = "Review to ensure this is intentional or safe." });
                }
                else if (threat.Severity == Severity.Info)
                {
                    score -= 2;
                }
            }

            score = Math.Max(0, score);
            stopwatch.Stop();

            return Task.FromResult(new ModuleResult
            {
                ModuleName = ModuleName,
                Success = true,
                Score = score,
                Grade = ScoreCalculator.GetGrade(score),
                Findings = globalFindings,
                Data = resultData,
                DurationMs = stopwatch.ElapsedMilliseconds
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ThreatDetector failed entirely.");
            stopwatch.Stop();
            return Task.FromResult(new ModuleResult
            {
                ModuleName = ModuleName,
                Success = false,
                Score = 0,
                Grade = "POOR",
                Findings = new List<Finding>(),
                Data = new ThreatScanResult(),
                DurationMs = stopwatch.ElapsedMilliseconds,
                ErrorMessage = ex.Message
            });
        }
    }
}
