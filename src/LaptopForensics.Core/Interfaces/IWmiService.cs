namespace LaptopForensics.Core.Interfaces;

/// <summary>
/// Provides WMI query operations for scanner modules.
/// </summary>
public interface IWmiService
{
    /// <summary>
    /// Executes a WMI query and returns all matching rows as key-value dictionaries.
    /// </summary>
    /// <param name="wmiClass">The WMI class name to query.</param>
    /// <param name="condition">Optional WQL WHERE condition.</param>
    /// <param name="namespacePath">Optional WMI namespace path.</param>
    /// <returns>Matching rows as dictionaries of property names and values.</returns>
    IEnumerable<Dictionary<string, object?>> Query(string wmiClass, string? condition = null, string? namespacePath = null);

    /// <summary>
    /// Returns a single typed property value from the first matching row.
    /// </summary>
    /// <typeparam name="T">Expected return type.</typeparam>
    /// <param name="wmiClass">The WMI class name to query.</param>
    /// <param name="property">The property name to read.</param>
    /// <param name="condition">Optional WQL WHERE condition.</param>
    /// <returns>The typed value if present; otherwise <c>null</c>.</returns>
    T? GetSingleValue<T>(string wmiClass, string property, string? condition = null);
}
