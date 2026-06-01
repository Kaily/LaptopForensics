using System.Management;

namespace LaptopForensics.Core.Services;

public class WmiService : IWmiService
{
    private readonly ILogger<WmiService> _logger;

    public WmiService(ILogger<WmiService> logger)
    {
        _logger = logger;
    }

    public IEnumerable<Dictionary<string, object?>> Query(
        string wmiClass,
        string? condition = null,
        string? namespacePath = null)
    {
        var rows = new List<Dictionary<string, object?>>();
        var ns = string.IsNullOrWhiteSpace(namespacePath) ? @"root\cimv2" : namespacePath;
        var wql = string.IsNullOrWhiteSpace(condition)
            ? $"SELECT * FROM {wmiClass}"
            : $"SELECT * FROM {wmiClass} WHERE {condition}";

        try
        {
            var scope = new ManagementScope(ns);
            scope.Connect();

            using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(wql));
            using var objects = searcher.Get();

            foreach (ManagementObject obj in objects)
            {
                var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (PropertyData property in obj.Properties)
                {
                    dict[property.Name] = property.Value;
                }

                rows.Add(dict);
            }
        }
        catch (ManagementException ex)
        {
            _logger.LogWarning(ex, "WMI query failed: {Wql} in namespace {Namespace}.", wql, ns);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected WMI query error: {Wql} in namespace {Namespace}.", wql, ns);
        }

        return rows;
    }

    public T? GetSingleValue<T>(string wmiClass, string property, string? condition = null)
    {
        var first = Query(wmiClass, condition).FirstOrDefault();
        if (first is null || !first.TryGetValue(property, out var value) || value is null)
        {
            return default;
        }

        try
        {
            if (value is T typed)
            {
                return typed;
            }

            return (T?)Convert.ChangeType(value, typeof(T));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to convert WMI property {Property} in class {Class}.", property, wmiClass);
            return default;
        }
    }
}
