namespace LaptopForensics.Core.Models;

public class HardwareInfo
{
    public CpuInfo Cpu { get; set; } = new();
    public List<RamSlot> Ram { get; set; } = new();
    public List<DiskInfo> Disks { get; set; } = new();
    public GpuInfo Gpu { get; set; } = new();
    public BatteryInfo Battery { get; set; } = new();
    public MotherboardInfo Motherboard { get; set; } = new();
    public double TotalRamGb => Ram.Sum(r => r.CapacityGb);
    public double FreeRamGb { get; set; }
    public double RamUsagePercent => TotalRamGb > 0 ? (1 - FreeRamGb / TotalRamGb) * 100 : 0;
}

public class CpuInfo
{
    public string Name { get; set; } = string.Empty;
    public int NumberOfCores { get; set; }
    public int NumberOfThreads { get; set; }
    public double MaxClockGhz { get; set; }
    public double CurrentClockGhz { get; set; }
    public int L2CacheMb { get; set; }
    public int L3CacheMb { get; set; }
    public string Architecture { get; set; } = string.Empty;
    public double? TemperatureCelsius { get; set; }
}

public class RamSlot
{
    public string BankLabel { get; set; } = string.Empty;
    public double CapacityGb { get; set; }
    public int SpeedMhz { get; set; }
    public string MemoryType { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string PartNumber { get; set; } = string.Empty;
}

public class DiskInfo
{
    public string Model { get; set; } = string.Empty;
    public string Interface { get; set; } = string.Empty;
    public double SizeGb { get; set; }
    public double FreeGb { get; set; }
    public int HealthPercent { get; set; }
    public int? TemperatureCelsius { get; set; }
    public long? PowerOnHours { get; set; }
    public string DriveLetter { get; set; } = string.Empty;
    public string FileSystem { get; set; } = string.Empty;
}

public class GpuInfo
{
    public string Name { get; set; } = string.Empty;
    public double VramGb { get; set; }
    public string DriverVersion { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;
    public double? TemperatureCelsius { get; set; }
}

public class BatteryInfo
{
    public bool IsPresent { get; set; }
    public double DesignCapacityWh { get; set; }
    public double CurrentCapacityWh { get; set; }
    public int ChargePercent { get; set; }
    public int HealthPercent { get; set; }
    public int? CycleCount { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class MotherboardInfo
{
    public string Manufacturer { get; set; } = string.Empty;
    public string Product { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string BiosVersion { get; set; } = string.Empty;
    public string BiosDate { get; set; } = string.Empty;
}
