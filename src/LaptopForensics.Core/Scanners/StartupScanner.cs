using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using LaptopForensics.Core.Helpers;
using LaptopForensics.Core.Interfaces;
using LaptopForensics.Core.Models;
using Microsoft.Extensions.Logging;

namespace LaptopForensics.Core.Scanners;

public class StartupScanner : IScanModule
{
    private readonly IRegistryService _registry;
    private readonly IPowerShellService _ps;
    private readonly ILogger<StartupScanner> _logger;

    public string ModuleName => "StartupScanner";
    public string ModuleIcon => "🚀";
    public int EstimatedSeconds => 8;
    public ScanMode ApplicableModes => ScanMode.Full | ScanMode.Quick;

    public StartupScanner(IRegistryService registry, IPowerShellService ps, ILogger<StartupScanner> logger)
    {
        _registry = registry;
        _ps = ps;
        _logger = logger;
    }

    public async Task<ModuleResult> ExecuteAsync(CancellationToken ct)
    {
        var resultData = new List<StartupItem>();
        var findings = new List<Finding>();
        double score = 100;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // 1. Registry Run Keys
            var regPaths = new[]
            {
                new { Hive = RegistryHive.LocalMachine, Path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run" },
                new { Hive = RegistryHive.CurrentUser, Path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run" },
                new { Hive = RegistryHive.LocalMachine, Path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce" },
                new { Hive = RegistryHive.CurrentUser, Path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce" }
            };

            foreach (var rp in regPaths)
            {
                try
                {
                    if (_registry.KeyExists(rp.Hive, rp.Path))
                    {
                        var values = _registry.GetAllValues(rp.Hive, rp.Path);
                        if (values != null)
                        {
                            foreach (var kvp in values)
                            {
                                var command = kvp.Value?.ToString() ?? "";
                                if (!string.IsNullOrWhiteSpace(command))
                                {
                                    resultData.Add(new StartupItem
                                    {
                                        Name = kvp.Key,
                                        Command = command,
                                        Location = $"{rp.Hive}\\{rp.Path}",
                                        IsEnabled = true,
                                        Category = DetermineCategory(kvp.Key, command),
                                        IsSuspicious = IsSuspicious(command)
                                    });
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to read registry key {rp.Path}");
                }
            }

            // 2. Startup Folders
            try
            {
                var userStartup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                var commonStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
                var folders = new[] { userStartup, commonStartup };

                foreach (var folder in folders)
                {
                    if (Directory.Exists(folder))
                    {
                        var files = Directory.GetFiles(folder);
                        foreach (var file in files)
                        {
                            var name = Path.GetFileName(file);
                            if (name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;

                            resultData.Add(new StartupItem
                            {
                                Name = name,
                                Command = file,
                                Location = folder,
                                IsEnabled = true,
                                Category = DetermineCategory(name, file),
                                IsSuspicious = IsSuspicious(file)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read startup folders");
            }

            // 3. Scheduled Tasks (startup trigger)
            try
            {
                // We'll use Get-ScheduledTask but getting triggers is tricky. We'll use schtasks or just get all ready tasks as requested by the prompt.
                // The prompt says: PowerShell Get-ScheduledTask | Where State -eq 'Ready' (take top 20 or count only if too slow, but try to extract names)
                // We'll limit it for speed.
                var psCmd = @"Get-ScheduledTask | Where-Object { $_.State -eq 'Ready' -or $_.State -eq 'Running' } | Select-Object -Property TaskName, TaskPath | Select-Object -First 30";
                var tasks = await _ps.RunAndGetObjectsAsync(psCmd, 15);
                if (tasks != null)
                {
                    foreach (var t in tasks)
                    {
                        var name = t.Properties["TaskName"]?.Value?.ToString() ?? "Unknown";
                        var path = t.Properties["TaskPath"]?.Value?.ToString() ?? "";
                        
                        // Exclude common Microsoft tasks to reduce noise
                        if (path.StartsWith(@"\Microsoft\Windows")) continue;

                        resultData.Add(new StartupItem
                        {
                            Name = name,
                            Command = "Scheduled Task",
                            Location = $"Task Scheduler: {path}",
                            IsEnabled = true,
                            Category = DetermineCategory(name, path),
                            IsSuspicious = false
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query scheduled tasks");
            }

            // 4. Services
            try
            {
                // Only Auto-Start services. Exclude common Windows services.
                var services = ServiceController.GetServices();
                foreach (var svc in services)
                {
                    // Basic filtering of Microsoft services by name (this is a simplified approach, real implementation would check binary path signature)
                    if (svc.ServiceName.StartsWith("Win", StringComparison.OrdinalIgnoreCase) || 
                        svc.ServiceName.StartsWith("Wdi", StringComparison.OrdinalIgnoreCase) ||
                        svc.ServiceName.StartsWith("Wbio", StringComparison.OrdinalIgnoreCase) ||
                        svc.ServiceName.StartsWith("Wpn", StringComparison.OrdinalIgnoreCase) ||
                        svc.ServiceName.StartsWith("App", StringComparison.OrdinalIgnoreCase))
                    {
                        continue; // Skip likely Windows services for brevity
                    }

                    if (svc.StartType == ServiceStartMode.Automatic)
                    {
                        resultData.Add(new StartupItem
                        {
                            Name = svc.DisplayName,
                            Command = "Windows Service",
                            Location = $"Service: {svc.ServiceName}",
                            IsEnabled = true,
                            Category = DetermineCategory(svc.DisplayName, svc.ServiceName),
                            IsSuspicious = false
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query Windows services");
            }

            // 5. Score and findings
            var suspiciousItems = resultData.Where(x => x.IsSuspicious).ToList();
            var bloatwareItems = resultData.Where(x => x.Category == StartupCategory.Bloatware).ToList();
            
            foreach (var item in suspiciousItems)
            {
                score -= 10;
                var evidence = new Dictionary<string, string>
                {
                    { "Executable", item.Command },
                    { "Location", item.Location },
                    { "Suspicion", "Command runs from a suspicious path or uses suspicious arguments." },
                    { "Action", item.Location.StartsWith("HK") ? $"reg delete \"{item.Location}\" /v \"{item.Name}\" /f" : $"Remove file: {item.Command}" }
                };

                findings.Add(new Finding 
                { 
                    Level = Severity.Critical, 
                    Title = $"Suspicious Auto-start item: \"{item.Name}\"", 
                    Description = "Found suspicious startup mechanism.", 
                    Recommendation = "Remove this entry to prevent persistent malicious execution.",
                    Confidence = ConfidenceLevel.High,
                    Evidence = evidence
                });
            }

            foreach (var item in bloatwareItems)
            {
                score -= 5;
                var evidence = new Dictionary<string, string>
                {
                    { "Executable", item.Command },
                    { "Location", item.Location },
                    { "Suspicion", "Known bloatware consuming system resources." },
                    { "Action", "Disable via Task Manager Startup tab or uninstall the software." }
                };

                findings.Add(new Finding 
                { 
                    Level = Severity.Warning, 
                    Title = $"Bloatware Auto-start item: \"{item.Name}\"", 
                    Description = "Bloatware launching at startup.", 
                    Recommendation = "Disable unnecessary startup applications to improve performance.",
                    Confidence = ConfidenceLevel.Medium,
                    Evidence = evidence
                });
            }

            if (resultData.Count > 30)
            {
                score -= 5;
                findings.Add(new Finding { Level = Severity.Info, Title = "Many startup items", Description = $"There are {resultData.Count} items configured to run at startup.", Recommendation = "Reduce the number of startup items to improve boot time." });
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
            _logger.LogError(ex, "StartupScanner failed entirely.");
            stopwatch.Stop();
            return new ModuleResult
            {
                ModuleName = ModuleName,
                Success = false,
                Score = 0,
                Grade = "POOR",
                Findings = new List<Finding>(),
                Data = new List<StartupItem>(),
                DurationMs = stopwatch.ElapsedMilliseconds,
                ErrorMessage = ex.Message
            };
        }
    }

    private StartupCategory DetermineCategory(string name, string command)
    {
        var combined = $"{name} {command}".ToLowerInvariant();

        var system = new[] { "intel", "amd", "nvidia", "realtek", "synaptics", "waves", "defender", "security" };
        if (system.Any(s => combined.Contains(s))) return StartupCategory.Essential;

        var useful = new[] { "onedrive", "dropbox", "google drive", "slack", "teams", "docker", "everything", "powertoys" };
        if (useful.Any(u => combined.Contains(u))) return StartupCategory.Useful;

        var optional = new[] { "spotify", "discord", "steam", "epic games", "origin", "gog", "skype", "webex", "zoom" };
        if (optional.Any(o => combined.Contains(o))) return StartupCategory.Optional;

        var bloatware = new[] { "mcafee", "norton", "avast", "hp support", "dell support", "lenovo vantage", "ccleaner", "driver booster" };
        if (bloatware.Any(b => combined.Contains(b))) return StartupCategory.Bloatware;

        return StartupCategory.Optional; // Default to optional rather than suspicious
    }

    private bool IsSuspicious(string command)
    {
        var lower = command.ToLowerInvariant();
        
        if (lower.Contains("temp\\") || lower.Contains("appdata\\local\\temp")) return true;
        if (lower.EndsWith(".vbs") || lower.EndsWith(".bat") || lower.EndsWith(".ps1") || lower.EndsWith(".cmd"))
        {
            // Allowed if in specific system directories, but typically suspicious in run keys
            if (!lower.Contains("system32") && !lower.Contains("syswow64"))
                return true;
        }
        
        if (lower.Contains("powershell") && (lower.Contains("-enc") || lower.Contains("-encodedcommand") || lower.Contains("-w hidden"))) return true;
        if (lower.Contains("cmd.exe /c") && lower.Contains("http")) return true;

        return false;
    }
}
