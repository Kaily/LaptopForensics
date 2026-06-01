using System;
using System.Linq;
using System.Threading.Tasks;
using LaptopForensics.Core.Interfaces;
using LaptopForensics.Core.Models;
using Spectre.Console;

namespace LaptopForensics.Export;

public class ConsoleReporter : IExporter, IScanProgressObserver
{
    public string FormatName => "Console";
    public string FileExtension => "";

    public Task ExportAsync(ScanReport report, string outputPath)
    {
        // Banner
        AnsiConsole.Write(
            new FigletText("LAPTOP FORENSICS")
                .Centered()
                .Color(Color.Blue));

        AnsiConsole.Write(new Rule($"[bold white]{report.Hostname}[/] | [grey]{report.ScanTimestamp}[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        // Summary Table
        var table = new Table().Centered();
        table.AddColumn("Module");
        table.AddColumn("Status");
        table.AddColumn("Score");
        table.AddColumn("Grade");
        table.AddColumn("Findings");
        table.AddColumn("Time (ms)");

        foreach (var kvp in report.Results)
        {
            var res = kvp.Value;
            var status = res.Success ? "[green]SUCCESS[/]" : "[red]FAILED[/]";
            var scoreStr = $"{res.Score:F1}";
            var gradeStr = GetGradeColor(res.Grade, res.Grade);
            
            table.AddRow(kvp.Key, status, scoreStr, gradeStr, res.Findings.Count.ToString(), res.DurationMs.ToString());
        }
        AnsiConsole.Write(table);

        // Overall Score Rule
        var overallColor = GetGradeColor(report.Grade, $"{report.OverallScore:F1} ({report.Grade})");
        AnsiConsole.Write(new Rule($"[bold]Overall Score: {overallColor}[/]").RuleStyle("blue"));
        AnsiConsole.WriteLine();

        // Per module Panels
        foreach (var kvp in report.Results)
        {
            var res = kvp.Value;
            if (res.Findings.Count == 0) continue;

            var topFindings = res.Findings.OrderByDescending(f => f.Level).Take(5).ToList();
            var contentBuilder = new System.Text.StringBuilder();

            foreach (var f in topFindings)
            {
                contentBuilder.AppendLine($"- [{GetSeverityColor(f.Level)}]{f.Level}[/]: [bold]{Markup.Escape(f.Title)}[/] (Confidence: {f.Confidence})");
                contentBuilder.AppendLine($"  [grey]{Markup.Escape(f.Description)}[/]");
                
                if (f.Evidence != null && f.Evidence.Count > 0)
                {
                    contentBuilder.AppendLine("  [underline]Evidence:[/]");
                    foreach (var ev in f.Evidence)
                    {
                        contentBuilder.AppendLine($"    - [teal]{Markup.Escape(ev.Key)}[/]: {Markup.Escape(ev.Value)}");
                    }
                }
                contentBuilder.AppendLine();
            }
            
            var panel = new Panel(contentBuilder.ToString().TrimEnd())
            {
                Header = new PanelHeader($" {kvp.Key} (Score: {res.Score:F1}) ", Justify.Left),
                Border = BoxBorder.Rounded,
                Padding = new Padding(1, 1, 1, 1)
            };
            AnsiConsole.Write(panel);
        }

        // Recommendations
        var recommendations = report.Results.SelectMany(kvp => kvp.Value.Findings)
                                            .Where(f => !string.IsNullOrWhiteSpace(f.Recommendation))
                                            .OrderByDescending(f => f.Level)
                                            .ToList();
        
        if (recommendations.Any())
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold underline]Top Actionable Recommendations:[/]");
            int index = 1;
            foreach (var rec in recommendations.Take(10))
            {
                AnsiConsole.MarkupLine($"{index}. [{GetSeverityColor(rec.Level)}]{rec.Level}[/]: {Markup.Escape(rec.Recommendation)}");
                index++;
            }
        }

        return Task.CompletedTask;
    }

    private string GetGradeColor(string grade, string text)
    {
        return grade.ToLower() switch
        {
            "excellent" => $"[green]{text}[/]",
            "good" => $"[blue]{text}[/]",
            "fair" => $"[yellow]{text}[/]",
            _ => $"[red]{text}[/]"
        };
    }

    private string GetSeverityColor(Severity level)
    {
        return level switch
        {
            Severity.Critical => "red",
            Severity.Warning => "yellow",
            _ => "green"
        };
    }

    // IScanProgressObserver Implementation
    public void OnModuleStarted(string moduleName, string icon, int estimatedSeconds)
    {
        AnsiConsole.MarkupLine($"[grey][[WAIT]][/] {icon} Starting [bold]{moduleName}[/]...");
    }

    public void OnModuleCompleted(string moduleName, ModuleResult result)
    {
        var mark = result.Success ? "[green]OK[/]" : "[red]FAIL[/]";
        AnsiConsole.MarkupLine($"[{mark}] {moduleName} completed in {result.DurationMs}ms - Score: {result.Score:F1} ({result.Grade})");
    }

    public void OnScanCompleted(ScanReport report)
    {
        // Trigger ExportAsync synchronously for console
        ExportAsync(report, "").GetAwaiter().GetResult();
    }
}
