using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using LaptopForensics.Core.Helpers;
using LaptopForensics.Core.Interfaces;
using LaptopForensics.Core.Models;
using Microsoft.Extensions.Logging;

namespace LaptopForensics.Core.Scanners;

public class PerformanceScanner : IScanModule
{
    private readonly IWmiService _wmi;
    private readonly ILogger<PerformanceScanner> _logger;

    public string ModuleName => "PerformanceScanner";
    public string ModuleIcon => "*";
    public int EstimatedSeconds => 6;
    public ScanMode ApplicableModes => ScanMode.Full | ScanMode.Quick;

    public PerformanceScanner(IWmiService wmi, ILogger<PerformanceScanner> logger)
    {
        _wmi = wmi;
        _logger = logger;
    }

    public Task<ModuleResult> ExecuteAsync(CancellationToken ct)
    {
        var resultData = new PerformanceMetrics();
        var findings = new List<Finding>();
        double score = 100;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // 1. CPU Usage
            try
            {
                using var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                cpuCounter.NextValue(); // First call always returns 0
                Thread.Sleep(500); // 500ms delay to get a reading
                resultData.CpuUsagePercent = Math.Round(cpuCounter.NextValue(), 2);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read CPU performance counter");
            }

            // 2. RAM Usage via GlobalMemoryStatusEx
            try
            {
                var memStatus = new MEMORYSTATUSEX();
                memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                if (GlobalMemoryStatusEx(ref memStatus))
                {
                    resultData.RamUsagePercent = memStatus.dwMemoryLoad;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read RAM via GlobalMemoryStatusEx");
            }

            // 3. Disk Usage
            try
            {
                using var diskCounter = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total");
                diskCounter.NextValue();
                Thread.Sleep(200);
                resultData.DiskUsagePercent = Math.Round(diskCounter.NextValue(), 2);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read Disk performance counter");
            }

            // 4. Page File Usage
            try
            {
                var pageFiles = _wmi.Query("Win32_PageFileUsage");
                double totalAllocated = 0;
                double totalUsage = 0;
                foreach (var pf in pageFiles)
                {
                    if (pf.ContainsKey("AllocatedBaseSize") && pf["AllocatedBaseSize"] != null &&
                        pf.ContainsKey("CurrentUsage") && pf["CurrentUsage"] != null)
                    {
                        if (double.TryParse(pf["AllocatedBaseSize"]?.ToString(), out double allocated) &&
                            double.TryParse(pf["CurrentUsage"]?.ToString(), out double usage))
                        {
                            totalAllocated += allocated;
                            totalUsage += usage;
                        }
                    }
                }
                
                if (totalAllocated > 0)
                {
                    resultData.PageFileUsagePercent = Math.Round((totalUsage / totalAllocated) * 100, 2);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read PageFile usage via WMI");
            }

            // 5. Uptime & Last Boot Time
            try
            {
                resultData.SystemUptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
                
                var osData = _wmi.Query("Win32_OperatingSystem").FirstOrDefault();
                if (osData != null && osData.ContainsKey("LastBootUpTime") && osData["LastBootUpTime"] != null)
                {
                    var lastBootStr = osData["LastBootUpTime"]?.ToString();
                    if (!string.IsNullOrEmpty(lastBootStr))
                    {
                        var parsed = DateTimeHelper.ParseWmiDate(lastBootStr);
                        if (parsed.HasValue)
                        {
                            resultData.LastBootTime = parsed.Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to determine Uptime");
            }

            // 6. Crashes (EventLog ID 41)
            try
            {
                int crashCount = 0;
                var cutoff = DateTime.Now.AddDays(-7);
                using var systemLog = new EventLog("System");
                
                for (int i = systemLog.Entries.Count - 1; i >= 0; i--)
                {
                    if (ct.IsCancellationRequested) break;
                    var entry = systemLog.Entries[i];
                    if (entry.TimeGenerated < cutoff) break;

                    if ((entry.InstanceId & 0xFFFF) == 41 && entry.Source == "Kernel-Power")
                    {
                        crashCount++;
                    }
                }
                resultData.CrashCount7Days = crashCount;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read EventLog for crashes");
            }

            // 7. Top Processes
            try
            {
                var processes = Process.GetProcesses();
                
                // Top RAM
                var topRam = processes
                    .OrderByDescending(p => p.WorkingSet64)
                    .Take(5)
                    .Select(p => new TopProcess
                    {
                        Name = p.ProcessName,
                        Pid = p.Id,
                        RamBytes = p.WorkingSet64,
                        CpuPercent = 0 // Expensive to calculate live CPU accurately without delays
                    })
                    .ToList();
                    
                resultData.TopRamProcesses = topRam;
                
                // Top CPU (rough estimate based on total processor time relative to uptime)
                var topCpuList = new List<TopProcess>();
                foreach (var p in processes)
                {
                    try
                    {
                        // Ignore idle and system processes
                        if (p.Id == 0 || p.Id == 4) continue;
                        
                        var uptime = DateTime.Now - p.StartTime;
                        if (uptime.TotalSeconds > 0)
                        {
                            var cpuPercent = (p.TotalProcessorTime.TotalSeconds / Environment.ProcessorCount) / uptime.TotalSeconds * 100;
                            topCpuList.Add(new TopProcess
                            {
                                Name = p.ProcessName,
                                Pid = p.Id,
                                RamBytes = p.WorkingSet64,
                                CpuPercent = Math.Round(cpuPercent, 2)
                            });
                        }
                    }
                    catch
                    {
                        // Access denied is common for some processes
                    }
                }
                
                resultData.TopCpuProcesses = topCpuList
                    .OrderByDescending(p => p.CpuPercent)
                    .Take(5)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect top processes");
            }

            // Calculate Score based on reasonable performance thresholds
            if (resultData.CpuUsagePercent > 90)
            {
                score -= 10;
                findings.Add(new Finding { Level = Severity.Warning, Title = "High CPU Usage", Description = $"CPU usage is at {resultData.CpuUsagePercent}%", Recommendation = "Check top processes for high CPU consumers." });
            }

            if (resultData.RamUsagePercent > 90)
            {
                score -= 10;
                findings.Add(new Finding { Level = Severity.Warning, Title = "High RAM Usage", Description = $"Memory usage is at {resultData.RamUsagePercent}%", Recommendation = "Close unused applications or consider upgrading RAM." });
            }

            if (resultData.DiskUsagePercent > 90)
            {
                score -= 10;
                findings.Add(new Finding { Level = Severity.Warning, Title = "High Disk Usage", Description = $"Disk usage is at {resultData.DiskUsagePercent}%", Recommendation = "Check for background disk-heavy tasks like indexing or updates." });
            }

            if (resultData.CrashCount7Days > 0)
            {
                int crashPenalty = Math.Min(resultData.CrashCount7Days * 5, 20);
                score -= crashPenalty;
                findings.Add(new Finding { Level = Severity.Critical, Title = "System Crashes Detected", Description = $"Found {resultData.CrashCount7Days} unexpected shutdowns (Event ID 41) in the last 7 days.", Recommendation = "Check hardware stability, thermal issues, or problematic drivers." });
            }
            
            if (resultData.SystemUptime.TotalDays > 14)
            {
                score -= 5;
                findings.Add(new Finding { Level = Severity.Info, Title = "Long System Uptime", Description = $"System has been running for {Math.Round(resultData.SystemUptime.TotalDays, 1)} days without a reboot.", Recommendation = "Reboot the system to install pending updates and clear memory." });
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
            _logger.LogError(ex, "PerformanceScanner failed entirely.");
            stopwatch.Stop();
            return Task.FromResult(new ModuleResult
            {
                ModuleName = ModuleName,
                Success = false,
                Score = 0,
                Grade = "POOR",
                Findings = new List<Finding>(),
                Data = new PerformanceMetrics(),
                DurationMs = stopwatch.ElapsedMilliseconds,
                ErrorMessage = ex.Message
            });
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
