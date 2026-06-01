using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LaptopForensics.Core.Helpers;
using LaptopForensics.Core.Interfaces;
using LaptopForensics.Core.Models;
using Microsoft.Extensions.Logging;

namespace LaptopForensics.Core.Scanners;

public class BrowserScanner : IScanModule
{
    private readonly ILogger<BrowserScanner> _logger;

    public string ModuleName => "BrowserScanner";
    public string ModuleIcon => "🌐";
    public int EstimatedSeconds => 5;
    public ScanMode ApplicableModes => ScanMode.Full | ScanMode.Quick;

    public BrowserScanner(ILogger<BrowserScanner> logger)
    {
        _logger = logger;
    }

    public Task<ModuleResult> ExecuteAsync(CancellationToken ct)
    {
        var resultData = new List<BrowserExtension>();
        var findings = new List<Finding>();
        double score = 100;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            
            // 1. Find Chrome Extensions
            var chromePaths = new[]
            {
                Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Extensions"),
                Path.Combine(localAppData, @"Google\Chrome\User Data\Profile 1\Extensions"),
                Path.Combine(localAppData, @"Google\Chrome\User Data\Profile 2\Extensions")
            };
            
            foreach (var path in chromePaths)
            {
                if (Directory.Exists(path))
                {
                    ScanChromiumExtensions(path, "Chrome", resultData);
                }
            }

            // 2. Find Edge Extensions
            var edgePaths = new[]
            {
                Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Extensions"),
                Path.Combine(localAppData, @"Microsoft\Edge\User Data\Profile 1\Extensions")
            };

            foreach (var path in edgePaths)
            {
                if (Directory.Exists(path))
                {
                    ScanChromiumExtensions(path, "Edge", resultData);
                }
            }

            // Deduplicate by browser and extension ID
            var uniqueExts = new List<BrowserExtension>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var ext in resultData)
            {
                var key = $"{ext.BrowserName}:{ext.ExtensionId}";
                if (!seen.Contains(key))
                {
                    seen.Add(key);
                    uniqueExts.Add(ext);
                }
            }
            resultData = uniqueExts;

            foreach (var ext in resultData)
            {
                var evidence = new Dictionary<string, string>
                {
                    { "Extension ID", ext.ExtensionId },
                    { "Version", ext.Version },
                    { "Install Date", ext.InstallDate.ToString("yyyy-MM-dd") },
                    { "Path", ext.InstallPath },
                    { "Permissions", string.Join(", ", ext.Permissions) },
                    { "Risk Score", $"{ext.RiskScore:F1}/10" },
                    { "Action", $"Remove extension manually from {ext.BrowserName} or delete folder: {ext.InstallPath}" }
                };

                if (ext.RiskScore >= 7.0 || ext.Risk == RiskLevel.High)
                {
                    score -= 10;
                    findings.Add(new Finding 
                    { 
                        Level = Severity.Critical, 
                        Title = $"{ext.BrowserName} Extension: \"{ext.Name}\" v{ext.Version}", 
                        Description = "High risk permissions detected.", 
                        Recommendation = "Immediate removal recommended due to dangerous permissions.",
                        Confidence = ConfidenceLevel.High,
                        Evidence = evidence
                    });
                }
                else if (ext.RiskScore >= 4.0 || ext.Risk == RiskLevel.Medium)
                {
                    score -= 5;
                    findings.Add(new Finding 
                    { 
                        Level = Severity.Warning, 
                        Title = $"{ext.BrowserName} Extension: \"{ext.Name}\" v{ext.Version}", 
                        Description = "Medium risk permissions detected.", 
                        Recommendation = "Review permissions for necessity.",
                        Confidence = ConfidenceLevel.Medium,
                        Evidence = evidence
                    });
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
                Findings = findings,
                Data = resultData,
                DurationMs = stopwatch.ElapsedMilliseconds
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BrowserScanner failed entirely.");
            stopwatch.Stop();
            return Task.FromResult(new ModuleResult
            {
                ModuleName = ModuleName,
                Success = false,
                Score = 0,
                Grade = "POOR",
                Findings = new List<Finding>(),
                Data = new List<BrowserExtension>(),
                DurationMs = stopwatch.ElapsedMilliseconds,
                ErrorMessage = ex.Message
            });
        }
    }

    private void ScanChromiumExtensions(string basePath, string browserName, List<BrowserExtension> results)
    {
        try
        {
            var extDirs = Directory.GetDirectories(basePath);
            foreach (var extDir in extDirs)
            {
                var extId = Path.GetFileName(extDir);
                var versionDirs = Directory.GetDirectories(extDir);
                
                // Get highest version dir (alphabetically usually works for SemVer, or just take the first)
                var latestVersionDir = versionDirs.OrderByDescending(d => d).FirstOrDefault();
                if (latestVersionDir != null)
                {
                    var manifestPath = Path.Combine(latestVersionDir, "manifest.json");
                    if (File.Exists(manifestPath))
                    {
                        var installDate = Directory.GetCreationTime(extDir);
                        ParseManifest(manifestPath, browserName, extId, installDate, extDir, results);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, $"Failed to scan Chromium extensions at {basePath}");
        }
    }

    private void ParseManifest(string manifestPath, string browserName, string extId, DateTime installDate, string installPath, List<BrowserExtension> results)
    {
        try
        {
            var json = File.ReadAllText(manifestPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var name = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var version = root.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "";
            var description = root.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
            
            // i18n names usually start with __MSG_
            if (name.StartsWith("__MSG_"))
            {
                name = extId; // fallback if localized
            }

            var permissions = new List<string>();
            if (root.TryGetProperty("permissions", out var pArray) && pArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in pArray.EnumerateArray())
                {
                    if (p.ValueKind == JsonValueKind.String)
                    {
                        permissions.Add(p.GetString() ?? "");
                    }
                }
            }

            var (riskLevel, riskScore) = DetermineRisk(permissions, installDate);

            var ext = new BrowserExtension
            {
                BrowserName = browserName,
                ExtensionId = extId,
                Name = name,
                Version = version,
                Description = description,
                IsEnabled = true, // We assume enabled if it's in the directory, though preferences file dictates actual state
                Permissions = permissions,
                Risk = riskLevel,
                RiskScore = riskScore,
                InstallDate = installDate,
                InstallPath = installPath
            };

            results.Add(ext);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, $"Failed to parse manifest {manifestPath}");
        }
    }

    private (RiskLevel, double) DetermineRisk(List<string> permissions, DateTime installDate)
    {
        double score = 0.0;

        bool hasTabs = permissions.Contains("tabs", StringComparer.OrdinalIgnoreCase);
        bool hasHistory = permissions.Contains("history", StringComparer.OrdinalIgnoreCase);
        bool hasWebRequest = permissions.Contains("webRequest", StringComparer.OrdinalIgnoreCase) || permissions.Contains("webRequestBlocking", StringComparer.OrdinalIgnoreCase);
        bool hasAllUrls = permissions.Contains("<all_urls>", StringComparer.OrdinalIgnoreCase) || permissions.Contains("*://*/*", StringComparer.OrdinalIgnoreCase);

        if (hasAllUrls || (hasTabs && hasHistory && hasWebRequest))
        {
            score += 3.0;
        }

        if (permissions.Contains("passwords", StringComparer.OrdinalIgnoreCase)) score += 2.5;
        if (hasHistory) score += 2.0;
        if (hasWebRequest) score += 1.5;

        if (installDate > DateTime.Now.AddDays(-7))
        {
            score += 1.0;
        }

        RiskLevel level = RiskLevel.Low;
        if (score >= 7.0) level = RiskLevel.High;
        else if (score >= 4.0) level = RiskLevel.Medium;

        return (level, Math.Min(10.0, score));
    }
}
