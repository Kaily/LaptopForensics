namespace LaptopForensics.Core.Models;

public class PerformanceMetrics
{
    public double CpuUsagePercent { get; set; }
    public double RamUsagePercent { get; set; }
    public double DiskUsagePercent { get; set; }
    public double PageFileUsagePercent { get; set; }
    public TimeSpan SystemUptime { get; set; }
    public DateTime LastBootTime { get; set; }
    public int CrashCount7Days { get; set; }
    public List<TopProcess> TopCpuProcesses { get; set; } = new();
    public List<TopProcess> TopRamProcesses { get; set; } = new();
}

public class TopProcess
{
    public string Name { get; set; } = string.Empty;
    public int Pid { get; set; }
    public double CpuPercent { get; set; }
    public long RamBytes { get; set; }
}
