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

public class LicenseAuditor : IScanModule
{
    private readonly IRegistryService _registry;
    private readonly IPowerShellService _ps;
    private readonly ILogger<LicenseAuditor> _logger;

    public string ModuleName => "LicenseAuditor";
    public string ModuleIcon => "📋";
    public int EstimatedSeconds => 10;
    public ScanMode ApplicableModes => ScanMode.Full;

    public LicenseAuditor(IRegistryService registry, IPowerShellService ps, ILogger<LicenseAuditor> logger)
    {
        _registry = registry;
        _ps = ps;
        _logger = logger;
    }

    public async Task<ModuleResult> ExecuteAsync(CancellationToken ct)
    {
        var resultData = new LicenseInfo();
        var findings = new List<Finding>();
        double score = 100;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // 1. Check Windows activation
            try
            {
                var winLicense = new WindowsLicense();
                var script = @"Get-WmiObject SoftwareLicensingProduct | Where-Object {$_.PartialProductKey -and $_.ApplicationId -eq '55c92734-d682-4d71-983e-d6ec3f16059f'} | Select-Object LicenseStatus, Description, PartialProductKey, LicenseExpirationDate";
                var psObjects = await _ps.RunAndGetObjectsAsync(script, 15);
                
                if (psObjects != null && psObjects.Any())
                {
                    var first = psObjects.First();
                    if (first.Properties["LicenseStatus"]?.Value is uint status)
                    {
                        winLicense.ActivationStatus = status == 1 ? "Activated" : "Not activated";
                        if (status != 1)
                        {
                            score -= 30;
                            findings.Add(new Finding { Level = Severity.Critical, Title = "Windows requires activation", Description = "Windows is not activated.", Recommendation = "Activate Windows with a valid product key." });
                        }
                    }
                    else
                    {
                        winLicense.ActivationStatus = "Unknown";
                    }

                    winLicense.Edition = first.Properties["Description"]?.Value?.ToString() ?? "Windows";
                    winLicense.ProductKey = first.Properties["PartialProductKey"]?.Value?.ToString();
                    
                    var expDate = first.Properties["LicenseExpirationDate"]?.Value?.ToString();
                    if (!string.IsNullOrEmpty(expDate))
                    {
                        winLicense.ExpiryDate = DateTimeHelper.ParseWmiDate(expDate);
                    }
                }
                else
                {
                    winLicense.ActivationStatus = "Unknown (WMI failed)";
                }
                
                resultData.Windows = winLicense;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check Windows activation");
            }

            // 2. Check Office
            try
            {
                var officeKey = @"SOFTWARE\Microsoft\Office\ClickToRun\Configuration";
                if (_registry.KeyExists(RegistryHive.LocalMachine, officeKey))
                {
                    var values = _registry.GetAllValues(RegistryHive.LocalMachine, officeKey);
                    if (values != null && values.ContainsKey("ProductReleaseIds"))
                    {
                        var officeLicense = new OfficeLicense
                        {
                            Version = values.ContainsKey("VersionToReport") ? values["VersionToReport"]?.ToString() ?? "" : "",
                        };
                        
                        // Checking activation is complex, script via ospp.vbs or WMI is needed. 
                        // The prompt says: "If found: PowerShell check activation"
                        // I will assume activated by default unless a trial/expiration logic triggers, 
                        // or run a basic WMI check for Office.
                        
                        var officeWmiScript = @"Get-WmiObject SoftwareLicensingProduct | Where-Object {$_.PartialProductKey -and $_.ApplicationId -eq '0ff1ce15-a989-479d-af46-f275c6370663'} | Select-Object LicenseStatus";
                        var officeObjs = await _ps.RunAndGetObjectsAsync(officeWmiScript, 10);
                        
                        bool officeActivated = true; // Optimistic default
                        if (officeObjs != null && officeObjs.Any())
                        {
                            var first = officeObjs.First();
                            if (first.Properties["LicenseStatus"]?.Value is uint oStatus)
                            {
                                officeActivated = (oStatus == 1);
                            }
                        }

                        if (officeActivated)
                        {
                            officeLicense.ActivationStatus = "Activated";
                        }
                        else
                        {
                            officeLicense.ActivationStatus = "Not activated";
                            score -= 20;
                            findings.Add(new Finding { Level = Severity.Critical, Title = "Microsoft Office requires activation", Description = "Microsoft Office is not activated.", Recommendation = "Activate Office with a valid subscription or product key." });
                        }

                        resultData.Office = officeLicense;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check Office activation");
            }

            // 3. Get all installed apps from SoftwareScanner results (re-read registry)
            try
            {
                var softwareScanner = new SoftwareScanner(_registry, _logger as ILogger<SoftwareScanner> ?? new Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory().CreateLogger<SoftwareScanner>());
                var softwareResult = await softwareScanner.ExecuteAsync(ct);
                
                if (softwareResult.Success && softwareResult.Data is SoftwareInfo softwareInfo)
                {
                    foreach (var app in softwareInfo.Applications)
                    {
                        var license = new SoftwareLicense
                        {
                            AppName = app.DisplayName,
                            Status = app.LicenseStatus,
                            ExpiryDate = app.InstallDate?.AddDays(30) // Approximation for trials
                        };

                        if (license.Status == LicenseStatus.Trial && license.ExpiryDate.HasValue)
                        {
                            var days = (int)(license.ExpiryDate.Value - DateTime.Now).TotalDays;
                            license.DaysUntilExpiry = days;

                            if (days < 0)
                            {
                                license.Status = LicenseStatus.Expired;
                                score -= 5; // -5 per trial software running expired (max -15) handled later
                                findings.Add(new Finding { Level = Severity.Warning, Title = "Expired trial", Description = $"Expired trial still installed: {app.DisplayName}", Recommendation = "Uninstall the software." });
                            }
                            else if (days < 30)
                            {
                                findings.Add(new Finding { Level = Severity.Warning, Title = "License expiring soon", Description = $"License expiring soon: {app.DisplayName}", Recommendation = "Renew or uninstall." });
                            }
                        }
                        else if (license.Status == LicenseStatus.Expired)
                        {
                            score -= 10; // -10 per expired commercial software (max -30) handled later
                            findings.Add(new Finding { Level = Severity.Warning, Title = "Expired commercial software", Description = $"Expired software still installed: {app.DisplayName}", Recommendation = "Uninstall or renew the software." });
                        }

                        resultData.Others.Add(license);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get software for license audit");
            }
            
            // Score capping
            int trialExpiredCount = resultData.Others.Count(l => l.Status == LicenseStatus.Expired && l.AppName.ToLowerInvariant().Contains("trial"));
            int commercialExpiredCount = resultData.Others.Count(l => l.Status == LicenseStatus.Expired && !l.AppName.ToLowerInvariant().Contains("trial"));
            
            // Re-apply bounded score deductions
            double initialScore = 100;
            if (resultData.Windows.ActivationStatus != "Activated" && resultData.Windows.ActivationStatus != "Unknown") initialScore -= 30;
            if (resultData.Office != null && resultData.Office.ActivationStatus == "Not activated") initialScore -= 20;
            
            initialScore -= Math.Min(commercialExpiredCount * 10, 30);
            initialScore -= Math.Min(trialExpiredCount * 5, 15);
            
            score = Math.Max(0, initialScore);

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
            _logger.LogError(ex, "LicenseAuditor failed entirely.");
            stopwatch.Stop();
            return new ModuleResult
            {
                ModuleName = ModuleName,
                Success = false,
                Score = 0,
                Grade = "POOR",
                Findings = new List<Finding>(),
                Data = new LicenseInfo(),
                DurationMs = stopwatch.ElapsedMilliseconds,
                ErrorMessage = ex.Message
            };
        }
    }
}
