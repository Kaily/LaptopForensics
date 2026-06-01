using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LaptopForensics.Core.Interfaces;
using LaptopForensics.Core.Models;

namespace LaptopForensics.Export;

public class CsvExporter : IExporter
{
    public string FormatName => "CSV";
    public string FileExtension => ".csv";

    public async Task ExportAsync(ScanReport report, string outputDirectory)
    {
        var timestamp = report.ScanTimestamp.ToString("yyyyMMdd_HHmmss");
        var baseFilename = $"{report.Hostname}_{timestamp}";
        
        var softwareFile = Path.Combine(outputDirectory, $"{baseFilename}_software{FileExtension}");
        var findingsFile = Path.Combine(outputDirectory, $"{baseFilename}_findings{FileExtension}");

        var encoding = new UTF8Encoding(true); // UTF-8 with BOM

        // 1. Export Software
        await using (var writer = new StreamWriter(softwareFile, false, encoding))
        {
            await writer.WriteLineAsync("Name,Version,Publisher,InstallDate,SizeGB,Category,LicenseStatus");
            
            if (report.Results.TryGetValue("Software", out var swResult) && swResult.Data is SoftwareInfo softwareInfo)
            {
                foreach (var sw in softwareInfo.Applications)
                {
                    var sizeGb = sw.EstimatedSizeBytes / (1024.0 * 1024.0 * 1024.0);
                    var line = string.Join(",", 
                        Escape(sw.DisplayName),
                        Escape(sw.Version),
                        Escape(sw.Publisher),
                        Escape(sw.InstallDate?.ToString("yyyy-MM-dd")),
                        sizeGb.ToString("F2"),
                        Escape(sw.Category.ToString()),
                        Escape(sw.LicenseStatus.ToString())
                    );
                    await writer.WriteLineAsync(line);
                }
            }
        }

        // 2. Export Findings
        await using (var writer = new StreamWriter(findingsFile, false, encoding))
        {
            await writer.WriteLineAsync("Module,Severity,Title,Description,Recommendation");
            
            var allFindings = report.Results.SelectMany(kvp => 
                kvp.Value.Findings.Select(f => new { Module = kvp.Key, Finding = f })
            ).OrderByDescending(x => x.Finding.Level);

            foreach (var item in allFindings)
            {
                var line = string.Join(",", 
                    Escape(item.Module),
                    item.Finding.Level.ToString(),
                    Escape(item.Finding.Title),
                    Escape(item.Finding.Description),
                    Escape(item.Finding.Recommendation)
                );
                await writer.WriteLineAsync(line);
            }
        }
    }

    private string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }
}
