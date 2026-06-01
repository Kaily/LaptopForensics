using Microsoft.Win32;

namespace LaptopForensics.Core.Services;

public class RegistryService : IRegistryService
{
    private readonly ILogger<RegistryService> _logger;

    public RegistryService(ILogger<RegistryService> logger)
    {
        _logger = logger;
    }

    public object? GetValue(RegistryHive hive, string keyPath, string valueName)
    {
        try
        {
            using var key = OpenSubKeyAnyView(hive, keyPath);
            return key?.GetValue(valueName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read registry value {Hive}\\{Path}\\{Name}.", hive, keyPath, valueName);
            return null;
        }
    }

    public IEnumerable<string> GetSubKeyNames(RegistryHive hive, string keyPath)
    {
        try
        {
            using var key = OpenSubKeyAnyView(hive, keyPath);
            return key?.GetSubKeyNames() ?? Array.Empty<string>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list registry subkeys for {Hive}\\{Path}.", hive, keyPath);
            return Array.Empty<string>();
        }
    }

    public Dictionary<string, object?> GetAllValues(RegistryHive hive, string keyPath)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var key = OpenSubKeyAnyView(hive, keyPath);
            if (key is null)
            {
                return values;
            }

            foreach (var valueName in key.GetValueNames())
            {
                values[valueName] = key.GetValue(valueName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read all values for registry key {Hive}\\{Path}.", hive, keyPath);
        }

        return values;
    }

    public bool KeyExists(RegistryHive hive, string keyPath)
    {
        try
        {
            using var key = OpenSubKeyAnyView(hive, keyPath);
            return key is not null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check registry key existence for {Hive}\\{Path}.", hive, keyPath);
            return false;
        }
    }

    private static RegistryKey? OpenSubKeyAnyView(RegistryHive hive, string keyPath)
    {
        RegistryKey? key;

        using (var base64 = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
        {
            key = base64.OpenSubKey(keyPath, writable: false);
            if (key is not null)
            {
                return key;
            }
        }

        using var base32 = RegistryKey.OpenBaseKey(hive, RegistryView.Registry32);
        return base32.OpenSubKey(keyPath, writable: false);
    }
}
