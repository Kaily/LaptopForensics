using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LaptopForensics.Core.Interfaces;
using LaptopForensics.Core.Models;

namespace LaptopForensics.Core.Orchestration;

public class TimedScanModule : IScanModule
{
    private readonly IScanModule _inner;
    private readonly IScanProgressObserver _observer;

    public TimedScanModule(IScanModule inner, IScanProgressObserver observer)
    {
        _inner = inner;
        _observer = observer;
    }

    public string ModuleName => _inner.ModuleName;
    public string ModuleIcon => _inner.ModuleIcon;
    public int EstimatedSeconds => _inner.EstimatedSeconds;
    public ScanMode ApplicableModes => _inner.ApplicableModes;

    public async Task<ModuleResult> ExecuteAsync(CancellationToken ct)
    {
        _observer.OnModuleStarted(ModuleName, ModuleIcon, EstimatedSeconds);
        var stopwatch = Stopwatch.StartNew();

        ModuleResult result;
        try
        {
            result = await _inner.ExecuteAsync(ct);
        }
        catch (Exception ex)
        {
            result = new ModuleResult
            {
                ModuleName = ModuleName,
                Success = false,
                Score = 0,
                Grade = "POOR",
                Findings = new System.Collections.Generic.List<Finding>(),
                Data = new object(),
                DurationMs = 0,
                ErrorMessage = $"Module threw an unhandled exception: {ex.Message}"
            };
        }
        finally
        {
            stopwatch.Stop();
        }

        result = result with { DurationMs = stopwatch.ElapsedMilliseconds };
        _observer.OnModuleCompleted(ModuleName, result);
        
        return result;
    }
}
