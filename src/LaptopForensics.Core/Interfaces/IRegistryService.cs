using Microsoft.Win32;

namespace LaptopForensics.Core.Interfaces;

/// <summary>
/// Provides safe registry read operations for scanner modules.
/// </summary>
public interface IRegistryService
{
    /// <summary>
    /// Gets a specific value from a registry key.
    /// </summary>
    /// <param name="hive">The registry hive to access.</param>
    /// <param name="keyPath">The subkey path within the hive.</param>
    /// <param name="valueName">The value name to retrieve.</param>
    /// <returns>The value if found; otherwise <c>null</c>.</returns>
    object? GetValue(RegistryHive hive, string keyPath, string valueName);

    /// <summary>
    /// Gets all immediate subkey names under the specified key path.
    /// </summary>
    /// <param name="hive">The registry hive to access.</param>
    /// <param name="keyPath">The subkey path within the hive.</param>
    /// <returns>A sequence of subkey names.</returns>
    IEnumerable<string> GetSubKeyNames(RegistryHive hive, string keyPath);

    /// <summary>
    /// Gets all value-name/value pairs from a registry key.
    /// </summary>
    /// <param name="hive">The registry hive to access.</param>
    /// <param name="keyPath">The subkey path within the hive.</param>
    /// <returns>A dictionary of all value entries for the key.</returns>
    Dictionary<string, object?> GetAllValues(RegistryHive hive, string keyPath);

    /// <summary>
    /// Determines whether the specified registry key exists.
    /// </summary>
    /// <param name="hive">The registry hive to access.</param>
    /// <param name="keyPath">The subkey path within the hive.</param>
    /// <returns><c>true</c> if the key exists; otherwise <c>false</c>.</returns>
    bool KeyExists(RegistryHive hive, string keyPath);
}
