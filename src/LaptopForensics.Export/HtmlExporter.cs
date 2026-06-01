using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LaptopForensics.Core.Interfaces;
using LaptopForensics.Core.Models;

namespace LaptopForensics.Export;

public class HtmlExporter : IExporter
{
    public string FormatName => "HTML";
    public string FileExtension => ".html";

    public async Task ExportAsync(ScanReport report, string outputDirectory)
    {
        var timestamp = report.ScanTimestamp.ToString("yyyyMMdd_HHmmss");
        var filename = $"{report.Hostname}_{timestamp}{FileExtension}";
        var filepath = Path.Combine(outputDirectory, filename);

        var html = new StringBuilder();
        
        // Basic HTML structure
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang='en'>");
        html.AppendLine("<head>");
        html.AppendLine($"<title>Laptop Forensics Report - {report.Hostname}</title>");
        html.AppendLine("<style>");
        html.AppendLine(@"
            body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #0d1117; color: #c9d1d9; margin: 0; padding: 20px; }
            h1, h2, h3, h4 { color: #ffffff; }
            .header { border-bottom: 2px solid #30363d; padding-bottom: 20px; margin-bottom: 30px; display: flex; justify-content: space-between; align-items: center; }
            .badge { display: inline-flex; align-items: center; justify-content: center; width: 100px; height: 100px; border-radius: 50%; font-size: 24px; font-weight: bold; color: white; }
            .badge.excellent { background-color: #2ea043; }
            .badge.good { background-color: #1f6feb; }
            .badge.fair { background-color: #d29922; }
            .badge.poor { background-color: #da3633; }
            .cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(250px, 1fr)); gap: 20px; margin-bottom: 40px; }
            .card { background-color: #161b22; border: 1px solid #30363d; border-radius: 6px; padding: 15px; }
            .card-title { font-size: 18px; font-weight: bold; margin-bottom: 10px; color: #1f6feb; }
            table { width: 100%; border-collapse: collapse; margin-bottom: 30px; background-color: #161b22; border: 1px solid #30363d; }
            th, td { padding: 10px; text-align: left; border-bottom: 1px solid #30363d; }
            th { background-color: #21262d; color: #ffffff; cursor: pointer; }
            tr:hover { background-color: #21262d; }
            .severity-high { color: #da3633; font-weight: bold; }
            .severity-medium { color: #d29922; font-weight: bold; }
            .severity-low { color: #2ea043; font-weight: bold; }
            .recommendations { background-color: #161b22; border-left: 4px solid #1f6feb; padding: 15px; border-radius: 4px; }
            .recommendations ol { margin: 0; padding-left: 20px; }
            .recommendations li { margin-bottom: 10px; }
        ");
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");

        // Header
        string badgeClass = report.Grade.ToLower() switch
        {
            "excellent" => "excellent",
            "good" => "good",
            "fair" => "fair",
            _ => "poor"
        };
        
        html.AppendLine("<div class='header'>");
        html.AppendLine("<div>");
        html.AppendLine($"<h1>Laptop Forensics Report</h1>");
        html.AppendLine($"<p><strong>Hostname:</strong> {report.Hostname}</p>");
        html.AppendLine($"<p><strong>OS Version:</strong> {report.OsVersion}</p>");
        html.AppendLine($"<p><strong>Scan Date:</strong> {report.ScanTimestamp:yyyy-MM-dd HH:mm:ss}</p>");
        html.AppendLine($"<p><strong>Scan Mode:</strong> {report.Mode}</p>");
        html.AppendLine("</div>");
        html.AppendLine($"<div class='badge {badgeClass}'>{report.OverallScore:F1}</div>");
        html.AppendLine("</div>");

        // Summary Cards
        html.AppendLine("<h2>Module Summaries</h2>");
        html.AppendLine("<div class='cards'>");
        foreach (var kvp in report.Results)
        {
            var res = kvp.Value;
            string cardColor = res.Grade.ToLower() switch
            {
                "excellent" => "#2ea043",
                "good" => "#1f6feb",
                "fair" => "#d29922",
                _ => "#da3633"
            };
            
            html.AppendLine("<div class='card'>");
            html.AppendLine($"<div class='card-title'>{kvp.Key}</div>");
            html.AppendLine($"<p><strong>Status:</strong> {(res.Success ? "Success" : "Failed")}</p>");
            html.AppendLine($"<p><strong>Score:</strong> <span style='color: {cardColor}'>{res.Score:F1} ({res.Grade})</span></p>");
            html.AppendLine($"<p><strong>Findings:</strong> {res.Findings.Count}</p>");
            html.AppendLine($"<p><strong>Time:</strong> {res.DurationMs}ms</p>");
            html.AppendLine("</div>");
        }
        html.AppendLine("</div>");

        // Findings Table
        var allFindings = report.Results.SelectMany(kvp => 
            kvp.Value.Findings.Select(f => new { Module = kvp.Key, Finding = f })
        ).OrderByDescending(x => x.Finding.Level).ToList();

        html.AppendLine("<h2>All Findings</h2>");
        if (allFindings.Any())
        {
            html.AppendLine("<table id='findingsTable'>");
            html.AppendLine("<thead><tr><th onclick='sortTable(0)'>Module</th><th onclick='sortTable(1)'>Severity</th><th onclick='sortTable(2)'>Title</th><th>Description</th></tr></thead>");
            html.AppendLine("<tbody>");
            foreach (var item in allFindings)
            {
                string sevClass = item.Finding.Level switch
                {
                    Severity.Critical => "severity-high",
                    Severity.Warning => "severity-medium",
                    _ => "severity-low"
                };
                
                html.AppendLine("<tr>");
                html.AppendLine($"<td>{item.Module}</td>");
                html.AppendLine($"<td class='{sevClass}'>{item.Finding.Level}</td>");
                html.AppendLine($"<td>{item.Finding.Title}</td>");
                html.AppendLine($"<td>{item.Finding.Description}</td>");
                html.AppendLine("</tr>");
            }
            html.AppendLine("</tbody></table>");
        }
        else
        {
            html.AppendLine("<p>No findings reported.</p>");
        }

        // Recommendations List
        var recommendations = allFindings.Where(x => !string.IsNullOrWhiteSpace(x.Finding.Recommendation)).ToList();
        if (recommendations.Any())
        {
            html.AppendLine("<h2>Actionable Recommendations</h2>");
            html.AppendLine("<div class='recommendations'><ol>");
            foreach (var item in recommendations)
            {
                string sevClass = item.Finding.Level switch
                {
                    Severity.Critical => "severity-high",
                    Severity.Warning => "severity-medium",
                    _ => "severity-low"
                };
                html.AppendLine($"<li><span class='{sevClass}'>[{item.Finding.Level}]</span> <strong>{item.Module}</strong>: {item.Finding.Recommendation}</li>");
            }
            html.AppendLine("</ol></div>");
        }

        // Add sorting script
        html.AppendLine(@"
        <script>
        function sortTable(n) {
          var table, rows, switching, i, x, y, shouldSwitch, dir, switchcount = 0;
          table = document.getElementById('findingsTable');
          if (!table) return;
          switching = true;
          dir = 'asc';
          while (switching) {
            switching = false;
            rows = table.rows;
            for (i = 1; i < (rows.length - 1); i++) {
              shouldSwitch = false;
              x = rows[i].getElementsByTagName('TD')[n];
              y = rows[i + 1].getElementsByTagName('TD')[n];
              if (dir == 'asc') {
                if (x.innerHTML.toLowerCase() > y.innerHTML.toLowerCase()) { shouldSwitch = true; break; }
              } else if (dir == 'desc') {
                if (x.innerHTML.toLowerCase() < y.innerHTML.toLowerCase()) { shouldSwitch = true; break; }
              }
            }
            if (shouldSwitch) {
              rows[i].parentNode.insertBefore(rows[i + 1], rows[i]);
              switching = true;
              switchcount ++;
            } else {
              if (switchcount == 0 && dir == 'asc') { dir = 'desc'; switching = true; }
            }
          }
        }
        </script>
        ");

        html.AppendLine("</body></html>");

        await File.WriteAllTextAsync(filepath, html.ToString(), Encoding.UTF8);
    }
}
