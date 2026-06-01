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

public class SoftwareScanner : IScanModule
{
    private readonly IRegistryService _registry;
    private readonly ILogger<SoftwareScanner> _logger;

    public string ModuleName => "SoftwareScanner";
    public string ModuleIcon => "*";
    public int EstimatedSeconds => 8;
    public ScanMode ApplicableModes => ScanMode.Full | ScanMode.Quick;

    public SoftwareScanner(IRegistryService registry, ILogger<SoftwareScanner> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    public Task<ModuleResult> ExecuteAsync(CancellationToken ct)
    {
        var resultData = new SoftwareInfo();
        var findings = new List<Finding>();
        double score = 100;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var apps = new List<InstalledApp>();

            // 1. Read from ALL 3 registry paths
            apps.AddRange(GetAppsFromRegistry(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", true));
            apps.AddRange(GetAppsFromRegistry(RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", false));
            apps.AddRange(GetAppsFromRegistry(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", true));

            // 3. Deduplicate: same DisplayName + Publisher = keep first
            var uniqueApps = new List<InstalledApp>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var app in apps)
            {
                var key = $"{app.DisplayName}|{app.Publisher}";
                if (!seen.Contains(key))
                {
                    seen.Add(key);
                    uniqueApps.Add(app);
                }
            }

            resultData.Applications = uniqueApps;

            // Score deductions
            int expiredCount = 0;
            int bloatwareCount = 0;
            int crackedCount = 0;

            foreach (var app in uniqueApps)
            {
                if (app.LicenseStatus == LicenseStatus.Expired)
                {
                    expiredCount++;
                }
                if (app.Category == AppCategory.Bloatware)
                {
                    bloatwareCount++;
                }
                if (app.LicenseStatus == LicenseStatus.Cracked)
                {
                    crackedCount++;
                }
            }

            int expiredPenalty = Math.Min(expiredCount * 5, 20);
            int bloatwarePenalty = Math.Min(bloatwareCount * 5, 15);
            int crackedPenalty = Math.Min(crackedCount * 3, 15);

            score -= expiredPenalty;
            score -= bloatwarePenalty;
            score -= crackedPenalty;

            if (uniqueApps.Count > 150)
            {
                score -= 2;
                findings.Add(new Finding { Level = Severity.Info, Title = "Cluttered system", Description = $"Total applications installed: {uniqueApps.Count}", Recommendation = "Consider uninstalling unused applications." });
            }

            if (expiredCount > 0)
            {
                findings.Add(new Finding { Level = Severity.Warning, Title = "Expired trials", Description = $"Found {expiredCount} expired trial applications.", Recommendation = "Uninstall or purchase licenses for expired trials." });
            }

            if (bloatwareCount > 0)
            {
                findings.Add(new Finding { Level = Severity.Info, Title = "Bloatware detected", Description = $"Found {bloatwareCount} bloatware applications.", Recommendation = "Consider uninstalling OEM bloatware for better performance." });
            }

            score = Math.Max(0, score);
            stopwatch.Stop();

            return Task.FromResult(new ModuleResult
            {
                ModuleName = ModuleName,
                Success = true,
                Score = score,
                Grade = ScoreCalculator.GetGrade(score),
                Findings = findings,
                Data = resultData,
                DurationMs = stopwatch.ElapsedMilliseconds
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SoftwareScanner failed entirely.");
            stopwatch.Stop();
            return Task.FromResult(new ModuleResult
            {
                ModuleName = ModuleName,
                Success = false,
                Score = 0,
                Grade = "POOR",
                Findings = new List<Finding>(),
                Data = new SoftwareInfo(),
                DurationMs = stopwatch.ElapsedMilliseconds,
                ErrorMessage = ex.Message
            });
        }
    }

    private IEnumerable<InstalledApp> GetAppsFromRegistry(RegistryHive hive, string basePath, bool is64Bit)
    {
        var apps = new List<InstalledApp>();
        var subKeyNames = _registry.GetSubKeyNames(hive, basePath);
        if (subKeyNames == null) return apps;

        foreach (var subKeyName in subKeyNames)
        {
            var keyPath = $@"{basePath}\{subKeyName}";
            var values = _registry.GetAllValues(hive, keyPath);
            
            if (values == null || !values.ContainsKey("DisplayName"))
                continue;

            var displayName = values["DisplayName"]?.ToString();
            
            // 2. Skip entries with no DisplayName
            if (string.IsNullOrWhiteSpace(displayName))
                continue;

            var app = new InstalledApp
            {
                DisplayName = displayName,
                Version = values.ContainsKey("DisplayVersion") ? values["DisplayVersion"]?.ToString() ?? "" : "",
                Publisher = values.ContainsKey("Publisher") ? values["Publisher"]?.ToString() ?? "" : "",
                InstallLocation = values.ContainsKey("InstallLocation") ? values["InstallLocation"]?.ToString() ?? "" : "",
                UninstallString = values.ContainsKey("UninstallString") ? values["UninstallString"]?.ToString() ?? "" : "",
                RegistrySource = hive.ToString(),
                Is64Bit = is64Bit
            };

            // 4. Parse InstallDate (format: "20240615")
            if (values.TryGetValue("InstallDate", out var installDateObj) && installDateObj != null)
            {
                var installDateStr = installDateObj.ToString();
                if (!string.IsNullOrEmpty(installDateStr) && installDateStr.Length == 8)
                {
                    if (int.TryParse(installDateStr.Substring(0, 4), out int year) &&
                        int.TryParse(installDateStr.Substring(4, 2), out int month) &&
                        int.TryParse(installDateStr.Substring(6, 2), out int day))
                    {
                        try
                        {
                            app.InstallDate = new DateTime(year, month, day);
                        }
                        catch { /* ignore invalid dates */ }
                    }
                }
            }

            // 5. EstimatedSize in KB -> Bytes
            if (values.TryGetValue("EstimatedSize", out var sizeObj) && sizeObj != null)
            {
                if (long.TryParse(sizeObj.ToString(), out long sizeKb))
                {
                    app.EstimatedSizeBytes = sizeKb * 1024;
                }
            }

            // 6. Categorize
            app.Category = DetermineCategory(app.DisplayName);

            // 7. License Status
            app.LicenseStatus = DetermineLicenseStatus(app);

            apps.Add(app);
        }

        return apps;
    }

    private AppCategory DetermineCategory(string name)
    {
        var lower = name.ToLowerInvariant();
        
        var bloatware = new[] { "mcafee", "norton", "avast", "hp support", "dell support", "lenovo", "cortana", "xbox", "your phone", "get office", "booking.com", "candy crush", "tiktok" };
        if (bloatware.Any(b => lower.Contains(b))) return AppCategory.Bloatware;

        var system = new[] { "microsoft", "windows", "visual c++", ".net" };
        if (system.Any(s => lower.Contains(s))) return AppCategory.System;

        var dev = new[] { "visual studio", "git", "docker", "python", "node", "jetbrains" };
        if (dev.Any(d => lower.Contains(d))) return AppCategory.Development;

        var productivity = new[] { "office", "teams", "slack", "zoom", "chrome", "firefox" };
        if (productivity.Any(p => lower.Contains(p))) return AppCategory.Productivity;

        return AppCategory.Unknown;
    }

    private LicenseStatus DetermineLicenseStatus(InstalledApp app)
    {
        var lower = app.DisplayName.ToLowerInvariant();
        bool isTrial = lower.Contains("trial") || lower.Contains("evaluation") || lower.Contains("demo") || lower.Contains("preview");

        if (isTrial)
        {
            if (app.InstallDate.HasValue && app.InstallDate.Value.AddDays(30) < DateTime.Now)
            {
                return LicenseStatus.Expired;
            }
            return LicenseStatus.Trial;
        }

        var freeApps = new[] { "git", "docker", "node", "chrome", "firefox", "slack", "zoom", "teams", "python" };
        if (freeApps.Any(f => lower.Contains(f)))
        {
            return LicenseStatus.Free;
        }

        if (app.Category == AppCategory.System)
        {
            return LicenseStatus.Free;
        }

        return LicenseStatus.Licensed;
    }
}
