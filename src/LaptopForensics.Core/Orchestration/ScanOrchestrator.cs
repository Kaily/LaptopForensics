using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LaptopForensics.Core.Interfaces;
using LaptopForensics.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LaptopForensics.Core.Orchestration;

public class ScanOrchestrator
{
    private readonly IEnumerable<IScanModule> _allModules;
    private readonly IScanRepository _repo;
    private readonly ScannerFactory _factory;
    private readonly IScanProgressObserver _observer;
    private readonly AppSettings _settings;
    private readonly ILogger<ScanOrchestrator> _logger;

    public ScanOrchestrator(
        IEnumerable<IScanModule> modules,
        IScanRepository repo,
        ScannerFactory factory,
        IScanProgressObserver observer,
        IOptions<AppSettings> settings,
        ILogger<ScanOrchestrator> logger)
    {
        _allModules = modules;
        _repo = repo;
        _factory = factory;
        _observer = observer;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<ScanReport> RunAsync(ScanMode mode, string? singleModuleName, CancellationToken ct)
    {
        var report = new ScanReport
        {
            Mode = mode
        };
        
        try
        {
            var osData = Environment.OSVersion.VersionString;
            report.OsVersion = osData;
        }
        catch
        {
            // fallback
        }

        var activeModules = _factory.Create(mode, _allModules, singleModuleName).ToList();

        if (!activeModules.Any())
        {
            _logger.LogWarning($"No scan modules selected for mode {mode} and module {singleModuleName}");
            return report;
        }

        var sw = Stopwatch.StartNew();

        foreach (var module in activeModules)
        {
            if (ct.IsCancellationRequested)
            {
                _logger.LogInformation("Scan cancelled by user.");
                break;
            }

            try
            {
                _observer.OnModuleStarted(module.ModuleName, "⚙️", 10);
                var result = await module.ExecuteAsync(ct);
                report.Results[module.ModuleName] = result;
                _observer.OnModuleCompleted(module.ModuleName, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Critical failure orchestrating module {module.ModuleName}");
                report.Results[module.ModuleName] = new ModuleResult
                {
                    ModuleName = module.ModuleName,
                    Success = false,
                    Score = 0,
                    Grade = "POOR",
                    Findings = new List<Finding>(),
                    Data = new object(),
                    DurationMs = 0,
                    ErrorMessage = "Orchestration failure: " + ex.Message
                };
            }
        }

        sw.Stop();
        report.TotalDurationMs = sw.ElapsedMilliseconds;

        try
        {
            // Serialize report for history
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = false });
            
            var history = new ScanHistory
            {
                ScanId = report.ScanId,
                ScanTimestamp = report.ScanTimestamp,
                Hostname = report.Hostname,
                OsVersion = report.OsVersion,
                ScanMode = report.Mode.ToString(),
                OverallScore = report.OverallScore,
                Grade = report.Grade,
                ReportJson = json,
                TotalDurationMs = report.TotalDurationMs,
                CreatedAt = DateTime.UtcNow
            };

            if (_settings.Storage != null && !string.IsNullOrWhiteSpace(_settings.Storage.DatabasePath))
            {
                await _repo.SaveAsync(history);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save scan history to repository");
        }

        _observer.OnScanCompleted(report);
        return report;
    }
}
