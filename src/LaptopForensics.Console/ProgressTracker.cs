using System;
using System.Diagnostics;

namespace LaptopForensics.Console;

public class ProgressTracker
{
    private readonly Stopwatch _stopwatch = new();
    private int _completedModules;
    private int _totalModules;

    public void Start(int totalModules)
    {
        _totalModules = totalModules;
        _completedModules = 0;
        _stopwatch.Restart();
    }

    public void Increment()
    {
        _completedModules++;
    }

    public TimeSpan CalculateEta()
    {
        if (_completedModules == 0 || _totalModules == 0)
            return TimeSpan.Zero;

        var elapsedMs = _stopwatch.ElapsedMilliseconds;
        var avgTimePerModule = elapsedMs / _completedModules;
        var remainingModules = _totalModules - _completedModules;
        var remainingMs = remainingModules * avgTimePerModule;

        return TimeSpan.FromMilliseconds(remainingMs);
    }

    public string FormatEta(TimeSpan eta)
    {
        if (eta.TotalSeconds < 1) return "~0 sec remaining";
        if (eta.TotalMinutes >= 1) return $"~{eta.Minutes} min {eta.Seconds} sec remaining";
        return $"~{eta.Seconds} sec remaining";
    }
}
