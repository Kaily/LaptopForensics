using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using LaptopForensics.Core.Interfaces;
using LaptopForensics.Core.Models;

namespace LaptopForensics.Export;

public class JsonExporter : IExporter
{
    public string FormatName => "JSON";
    public string FileExtension => ".json";

    public async Task ExportAsync(ScanReport report, string outputDirectory)
    {
        var timestamp = report.ScanTimestamp.ToString("yyyyMMdd_HHmmss");
        var filename = $"{report.Hostname}_{timestamp}{FileExtension}";
        var filepath = Path.Combine(outputDirectory, filename);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReferenceHandler = ReferenceHandler.Preserve
        };

        await using var stream = File.Create(filepath);
        await JsonSerializer.SerializeAsync(stream, report, options);

    }
}
