namespace LaptopForensics.Core.Scanners;

public class HardwareScanner : IScanModule
{
    private readonly IWmiService _wmi;
    private readonly ILogger<HardwareScanner> _logger;

    public HardwareScanner(IWmiService wmi, ILogger<HardwareScanner> logger)
    {
        _wmi = wmi;
        _logger = logger;
    }

    public string ModuleName => "HardwareScanner";
    public string ModuleIcon => "\ud83d\udda5\ufe0f";
    public int EstimatedSeconds => 8;
    public ScanMode ApplicableModes => ScanMode.Full | ScanMode.Quick;

    public Task<ModuleResult> ExecuteAsync(CancellationToken ct)
    {
        var started = DateTime.UtcNow;
        var hardware = new HardwareInfo();

        try
        {
            ct.ThrowIfCancellationRequested();
            ReadCpu(hardware);
            ReadRam(hardware);
            ReadDisks(hardware);
            ReadGpu(hardware);
            ReadBattery(hardware);
            ReadMotherboard(hardware);

            var findings = BuildFindings(hardware);
            var score = CalculateScore(hardware);

            return Task.FromResult(new ModuleResult
            {
                ModuleName = ModuleName,
                Success = true,
                Score = score,
                Grade = Helpers.ScoreCalculator.GetGrade(score),
                Findings = findings,
                Data = hardware,
                DurationMs = ElapsedMs(started)
            });
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Hardware scan was cancelled.");
            return Task.FromResult(FailedResult(hardware, "Cancelled", started));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hardware scan failed unexpectedly.");
            return Task.FromResult(FailedResult(hardware, "Hardware module incomplete: WMI unavailable", started));
        }
    }

    private void ReadCpu(HardwareInfo hardware)
    {
        try
        {
            var cpu = _wmi.Query("Win32_Processor").FirstOrDefault();
            if (cpu is null)
            {
                return;
            }

            hardware.Cpu = new CpuInfo
            {
                Name = GetString(cpu, "Name"),
                NumberOfCores = GetInt(cpu, "NumberOfCores"),
                NumberOfThreads = GetInt(cpu, "NumberOfLogicalProcessors"),
                MaxClockGhz = GetInt(cpu, "MaxClockSpeed") / 1000d,
                CurrentClockGhz = GetInt(cpu, "CurrentClockSpeed") / 1000d,
                L2CacheMb = GetInt(cpu, "L2CacheSize") / 1024,
                L3CacheMb = GetInt(cpu, "L3CacheSize") / 1024,
                Architecture = Environment.Is64BitOperatingSystem ? "x64" : "x86"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read CPU information.");
        }
    }

    private void ReadRam(HardwareInfo hardware)
    {
        try
        {
            foreach (var item in _wmi.Query("Win32_PhysicalMemory"))
            {
                hardware.Ram.Add(new RamSlot
                {
                    BankLabel = GetString(item, "BankLabel"),
                    CapacityGb = Math.Round(GetLong(item, "Capacity") / 1024d / 1024d / 1024d, 2),
                    SpeedMhz = GetInt(item, "Speed"),
                    MemoryType = MapMemoryType(GetInt(item, "MemoryType")),
                    Manufacturer = GetString(item, "Manufacturer"),
                    PartNumber = GetString(item, "PartNumber").Trim()
                });
            }

            var freeKb = _wmi.GetSingleValue<ulong>("Win32_OperatingSystem", "FreePhysicalMemory");
            hardware.FreeRamGb = Math.Round(freeKb / 1024d / 1024d, 2);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read RAM information.");
        }
    }

    private void ReadDisks(HardwareInfo hardware)
    {
        try
        {
            var physicalDisks = _wmi.Query("Win32_DiskDrive").ToList();
            var fallbackDisk = physicalDisks.FirstOrDefault();

            foreach (var logical in _wmi.Query("Win32_LogicalDisk", "DriveType = 3"))
            {
                var sizeBytes = GetLong(logical, "Size");
                var freeBytes = GetLong(logical, "FreeSpace");

                hardware.Disks.Add(new DiskInfo
                {
                    Model = GetString(fallbackDisk, "Model"),
                    Interface = GetString(fallbackDisk, "InterfaceType"),
                    SizeGb = Math.Round(sizeBytes / 1024d / 1024d / 1024d, 2),
                    FreeGb = Math.Round(freeBytes / 1024d / 1024d / 1024d, 2),
                    HealthPercent = 100,
                    DriveLetter = GetString(logical, "DeviceID"),
                    FileSystem = GetString(logical, "FileSystem")
                });
            }

            if (hardware.Disks.Count == 0)
            {
                foreach (var disk in physicalDisks)
                {
                    hardware.Disks.Add(new DiskInfo
                    {
                        Model = GetString(disk, "Model"),
                        Interface = GetString(disk, "InterfaceType"),
                        SizeGb = Math.Round(GetLong(disk, "Size") / 1024d / 1024d / 1024d, 2),
                        HealthPercent = 100
                    });
                }
            }

            ApplySmartHealth(hardware);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read disk information.");
        }
    }

    private void ApplySmartHealth(HardwareInfo hardware)
    {
        try
        {
            var smartStatuses = _wmi.Query(
                "MSStorageDriver_FailurePredictStatus",
                namespacePath: @"root\wmi").ToList();

            if (smartStatuses.Count == 0)
            {
                return;
            }

            var predictedFailure = smartStatuses.Any(s => GetBool(s, "PredictFailure"));
            foreach (var disk in hardware.Disks)
            {
                disk.HealthPercent = predictedFailure ? 50 : 100;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read SMART health information.");
        }
    }

    private void ReadGpu(HardwareInfo hardware)
    {
        try
        {
            var gpu = _wmi.Query("Win32_VideoController").FirstOrDefault();
            if (gpu is null)
            {
                return;
            }

            var width = GetInt(gpu, "CurrentHorizontalResolution");
            var height = GetInt(gpu, "CurrentVerticalResolution");
            hardware.Gpu = new GpuInfo
            {
                Name = GetString(gpu, "Name"),
                VramGb = Math.Round(GetLong(gpu, "AdapterRAM") / 1024d / 1024d / 1024d, 2),
                DriverVersion = GetString(gpu, "DriverVersion"),
                Resolution = width > 0 && height > 0 ? $"{width}x{height}" : string.Empty
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read GPU information.");
        }
    }

    private void ReadBattery(HardwareInfo hardware)
    {
        try
        {
            var battery = _wmi.Query("Win32_Battery").FirstOrDefault();
            if (battery is null)
            {
                hardware.Battery.IsPresent = false;
                return;
            }

            var design = GetDouble(battery, "DesignCapacity");
            var current = GetDouble(battery, "FullChargeCapacity");
            var charge = GetInt(battery, "EstimatedChargeRemaining");

            hardware.Battery = new BatteryInfo
            {
                IsPresent = true,
                DesignCapacityWh = design,
                CurrentCapacityWh = current,
                ChargePercent = charge,
                HealthPercent = design > 0 ? (int)Math.Round(current / design * 100) : 100,
                CycleCount = GetNullableInt(battery, "CycleCount"),
                Status = MapBatteryStatus(GetInt(battery, "BatteryStatus"))
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read battery information.");
        }
    }

    private void ReadMotherboard(HardwareInfo hardware)
    {
        try
        {
            var board = _wmi.Query("Win32_BaseBoard").FirstOrDefault();
            var bios = _wmi.Query("Win32_BIOS").FirstOrDefault();

            hardware.Motherboard = new MotherboardInfo
            {
                Manufacturer = GetString(board, "Manufacturer"),
                Product = GetString(board, "Product"),
                SerialNumber = GetString(board, "SerialNumber"),
                BiosVersion = GetString(bios, "Version"),
                BiosDate = Helpers.DateTimeHelper.ParseWmiDate(GetString(bios, "ReleaseDate"))?.ToString("yyyy-MM-dd") ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read motherboard information.");
        }
    }

    private static double CalculateScore(HardwareInfo hardware)
    {
        var score = 100d;

        if (hardware.Disks.Any(d => d.HealthPercent < 80))
        {
            score -= 20;
        }

        if (hardware.Cpu.TemperatureCelsius > 90)
        {
            score -= 15;
        }

        if (hardware.RamUsagePercent > 90)
        {
            score -= 10;
        }

        if (hardware.Disks.Any(d => d.FreeGb < 5))
        {
            score -= 10;
        }

        if (hardware.Battery.IsPresent && hardware.Battery.HealthPercent < 70)
        {
            score -= 10;
        }

        if (hardware.Battery.IsPresent && hardware.Battery.HealthPercent < 85)
        {
            score -= 5;
        }

        if (hardware.Disks.Any(d => d.HealthPercent < 95))
        {
            score -= 5;
        }

        return Math.Clamp(score, 0, 100);
    }

    private static List<Finding> BuildFindings(HardwareInfo hardware)
    {
        var findings = new List<Finding>();

        foreach (var disk in hardware.Disks.Where(d => d.HealthPercent < 70))
        {
            findings.Add(CreateFinding(Severity.Critical, "Disk failure imminent", $"{disk.DriveLetter} {disk.Model} health is {disk.HealthPercent}%.", "Back up data immediately and replace the disk."));
        }

        if (hardware.Cpu.TemperatureCelsius > 95)
        {
            findings.Add(CreateFinding(Severity.Critical, "CPU overheating", $"CPU temperature is {hardware.Cpu.TemperatureCelsius:0.#} C.", "Clean cooling vents, verify fan operation, and reduce load until temperatures normalize."));
        }

        foreach (var disk in hardware.Disks.Where(d => d.FreeGb < 5))
        {
            findings.Add(CreateFinding(Severity.Warning, "Low disk space", $"{disk.DriveLetter} has only {disk.FreeGb:0.##} GB free.", "Free up disk space or move data to another drive."));
        }

        if (hardware.Battery.IsPresent && hardware.Battery.HealthPercent < 70)
        {
            findings.Add(CreateFinding(Severity.Warning, "Battery degraded", $"Battery health is {hardware.Battery.HealthPercent}%.", "Plan battery replacement."));
        }

        if (hardware.RamUsagePercent > 85)
        {
            findings.Add(CreateFinding(Severity.Warning, "High memory usage", $"RAM usage is {hardware.RamUsagePercent:0.#}%.", "Close unused applications or consider adding memory."));
        }

        if (hardware.Battery.IsPresent && hardware.Battery.CycleCount > 500)
        {
            findings.Add(CreateFinding(Severity.Info, "Battery aging", $"Battery cycle count is {hardware.Battery.CycleCount}.", "Monitor battery runtime and replacement timing."));
        }

        foreach (var disk in hardware.Disks.Where(d => d.HealthPercent < 95))
        {
            findings.Add(CreateFinding(Severity.Info, "Disk showing minor wear", $"{disk.DriveLetter} {disk.Model} health is {disk.HealthPercent}%.", "Monitor disk health regularly."));
        }

        return findings;
    }

    private static Finding CreateFinding(Severity severity, string title, string description, string recommendation)
    {
        return new Finding
        {
            Level = severity,
            Title = title,
            Description = description,
            Recommendation = recommendation
        };
    }

    private static ModuleResult FailedResult(HardwareInfo hardware, string message, DateTime started)
    {
        return new ModuleResult
        {
            ModuleName = "HardwareScanner",
            Success = false,
            Score = 0,
            Grade = "POOR",
            Findings = new List<Finding>
            {
                CreateFinding(Severity.Warning, "Hardware scan incomplete", message, "Run the application as administrator and verify WMI service health.")
            },
            Data = hardware,
            DurationMs = ElapsedMs(started),
            ErrorMessage = message
        };
    }

    private static long ElapsedMs(DateTime started)
    {
        return (long)(DateTime.UtcNow - started).TotalMilliseconds;
    }

    private static string GetString(Dictionary<string, object?>? values, string key)
    {
        if (values is null || !values.TryGetValue(key, out var value) || value is null)
        {
            return string.Empty;
        }

        return Convert.ToString(value) ?? string.Empty;
    }

    private static int GetInt(Dictionary<string, object?> values, string key)
    {
        return (int)Math.Round(GetDouble(values, key));
    }

    private static int? GetNullableInt(Dictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return GetInt(values, key);
    }

    private static long GetLong(Dictionary<string, object?>? values, string key)
    {
        if (values is null || !values.TryGetValue(key, out var value) || value is null)
        {
            return 0;
        }

        try
        {
            return Convert.ToInt64(value);
        }
        catch
        {
            return 0;
        }
    }

    private static double GetDouble(Dictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || value is null)
        {
            return 0;
        }

        try
        {
            return Convert.ToDouble(value);
        }
        catch
        {
            return 0;
        }
    }

    private static bool GetBool(Dictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || value is null)
        {
            return false;
        }

        try
        {
            return Convert.ToBoolean(value);
        }
        catch
        {
            return false;
        }
    }

    private static string MapMemoryType(int type)
    {
        return type switch
        {
            20 => "DDR",
            21 => "DDR2",
            24 => "DDR3",
            26 => "DDR4",
            34 => "DDR5",
            _ => type > 0 ? $"Type {type}" : string.Empty
        };
    }

    private static string MapBatteryStatus(int status)
    {
        return status switch
        {
            1 => "Discharging",
            2 => "Charging",
            3 => "Full",
            6 => "Charging",
            7 => "Charging",
            8 => "Charging",
            9 => "Charging",
            _ => string.Empty
        };
    }
}
