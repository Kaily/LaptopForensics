using System;
using System.Collections.Generic;
using LaptopForensics.Core.Interfaces;
using LaptopForensics.Core.Models;
using Spectre.Console;

namespace LaptopForensics.Console;

public class ConsoleUI : IScanProgressObserver
{
    private readonly ProgressTracker _tracker = new();

    public void ShowBanner()
    {
        AnsiConsole.Write(
            new FigletText("FORENSICS")
                .Centered()
                .Color(Color.Blue));
                
        AnsiConsole.MarkupLine("[bold cyan]Laptop Forensics System[/]");
        AnsiConsole.MarkupLine($"[grey]Host:[/] {Environment.MachineName} | [grey]OS:[/] {Environment.OSVersion.VersionString}");
        AnsiConsole.WriteLine();
    }

    public void ShowScanStarting(ScanMode mode, int moduleCount)
    {
        AnsiConsole.MarkupLine($"[yellow]Starting {mode} scan...[/] [grey]({moduleCount} modules queued)[/]");
        AnsiConsole.WriteLine();
    }

    public void ShowHistory(IEnumerable<ScanHistory> history)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("ID");
        table.AddColumn("Timestamp");
        table.AddColumn("Mode");
        table.AddColumn("Modules");
        table.AddColumn("Findings");
        table.AddColumn("Duration (s)");
        table.AddColumn("OS");

        foreach (var h in history)
        {
            table.AddRow(
                h.ScanId,
                h.ScanTimestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                h.ScanMode,
                "-",
                h.Grade,
                (h.TotalDurationMs / 1000.0).ToString("F1"),
                h.OsVersion
            );
        }

        AnsiConsole.Write(table);
    }

    public void ShowHelp()
    {
        AnsiConsole.MarkupLine("[bold cyan]Laptop Forensics CLI Usage[/]");
        AnsiConsole.MarkupLine("  [green]--quick[/]            Run quick scan mode");
        AnsiConsole.MarkupLine("  [green]--module <name>[/]    Run a specific module");
        AnsiConsole.MarkupLine("  [green]--export <format>[/]  Export format (json, csv, html, all)");
        AnsiConsole.MarkupLine("  [green]--output <path>[/]    Output directory for reports");
        AnsiConsole.MarkupLine("  [green]--silent[/]           Run without console output");
        AnsiConsole.MarkupLine("  [green]--watch[/]            Run in watch mode (loop)");
        AnsiConsole.MarkupLine("  [green]--interval <min>[/]   Delay between watch loops in minutes (default: 60)");
        AnsiConsole.MarkupLine("  [green]--history[/]          View scan history");
        AnsiConsole.MarkupLine("  [green]--help[/]             Show this help text");
    }

    public void OnScanStarted(ScanMode mode, int totalModules)
    {
        _tracker.Start(totalModules);
        ShowScanStarting(mode, totalModules);
    }

    public void OnModuleStarted(string moduleName, string icon, int estimatedSeconds)
    {
        AnsiConsole.Markup($"[grey]>[/] {icon} Running [white]{moduleName}[/]... ");
    }

    public void OnModuleCompleted(string moduleName, ModuleResult result)
    {
        _tracker.Increment();
        var eta = _tracker.FormatEta(_tracker.CalculateEta());

        if (result.Success)
        {
            var findings = result.Findings.Count > 0 
                ? $"[red]{result.Findings.Count} findings[/]" 
                : "[green]Clean[/]";
            
            AnsiConsole.MarkupLine($"[green]DONE[/] ({result.DurationMs}ms) - {findings} - [grey]{eta}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]FAILED[/] ({result.DurationMs}ms) - {result.ErrorMessage} - [grey]{eta}[/]");
        }
    }

    public void OnScanCompleted(ScanReport report)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold green]Scan completed in {report.TotalDurationMs / 1000.0:F1}s.[/]");
        var totalFindings = report.Results.Values.Sum(r => r.Findings.Count);
        AnsiConsole.MarkupLine($"Total Findings: [red]{totalFindings}[/]");
        AnsiConsole.WriteLine();
    }

    public void OnError(string message, Exception ex)
    {
        AnsiConsole.MarkupLine($"[bold red]ERROR:[/] {message}");
        if (ex != null)
        {
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
        }
    }
}
