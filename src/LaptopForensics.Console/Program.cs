using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LaptopForensics.Core.Interfaces;
using LaptopForensics.Core.Models;
using LaptopForensics.Core.Orchestration;
using LaptopForensics.Core.Scanners;
using LaptopForensics.Data;
using LaptopForensics.Export;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Spectre.Console;

namespace LaptopForensics.Console;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var ui = new ConsoleUI();

        bool isHelp = args.Contains("--help");
        bool isVersion = args.Contains("--version");
        bool isHistory = args.Contains("--history");
        bool isQuick = args.Contains("--quick");
        bool isSilent = args.Contains("--silent");
        bool isWatch = args.Contains("--watch");
        
        string moduleName = GetArgValue(args, "--module");
        string exportFormat = GetArgValue(args, "--export") ?? "console";
        string outputPath = GetArgValue(args, "--output") ?? Path.Combine(Directory.GetCurrentDirectory(), "Reports");
        int watchIntervalMin = int.TryParse(GetArgValue(args, "--interval"), out var val) ? val : 60;

        if (isHelp)
        {
            ui.ShowBanner();
            ui.ShowHelp();
            return 0;
        }

        if (isVersion)
        {
            AnsiConsole.WriteLine("Laptop Forensics System v1.0.0");
            return 0;
        }

        if (!isSilent)
        {
            ui.ShowBanner();
        }

        bool isAdmin = new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent())
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        
        if (!isAdmin)
        {
            AnsiConsole.MarkupLine("[bold yellow]WARNING:[/] Program is not running as Administrator. Some scans may return incomplete data.");
            AnsiConsole.WriteLine();
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        try
        {
            var services = new ServiceCollection();

            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddSerilog(dispose: true);
            });

            ConfigureServices(services, configuration);
            
            // Add exporters based on args
            if (exportFormat.Contains("json") || exportFormat == "all") services.AddTransient<IExporter, JsonExporter>();
            if (exportFormat.Contains("csv") || exportFormat == "all") services.AddTransient<IExporter, CsvExporter>();
            if (exportFormat.Contains("html") || exportFormat == "all") services.AddTransient<IExporter, HtmlExporter>();
            if (exportFormat.Contains("console") || exportFormat == "all" || !isSilent) services.AddTransient<IExporter, ConsoleReporter>();

            // Add the UI observer if not silent
            if (!isSilent)
            {
                services.AddSingleton<IScanProgressObserver>(ui);
            }

            await using var serviceProvider = services.BuildServiceProvider();
            
            if (isHistory)
            {
                var repo = serviceProvider.GetRequiredService<IScanRepository>();
                var history = await repo.GetAllAsync();
                ui.ShowHistory(history);
                return 0;
            }

            var orchestrator = serviceProvider.GetRequiredService<ScanOrchestrator>();
            var mode = isQuick ? ScanMode.Quick : ScanMode.Full;
            var cts = new CancellationTokenSource();

            System.Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            if (isWatch)
            {
                AnsiConsole.MarkupLine($"[cyan]Running in Watch Mode. Interval: {watchIntervalMin} minutes. Press Ctrl+C to stop.[/]");
                while (!cts.IsCancellationRequested)
                {
                    var report = await orchestrator.RunAsync(mode, moduleName, cts.Token);
                    var exporters = serviceProvider.GetServices<IExporter>();
                    if (!System.IO.Directory.Exists(outputPath))
                    {
                        System.IO.Directory.CreateDirectory(outputPath);
                    }
                    foreach (var exp in exporters)
                    {
                        await exp.ExportAsync(report, outputPath);
                    }
                    
                    if (!cts.IsCancellationRequested)
                    {
                        AnsiConsole.MarkupLine($"[grey]Sleeping for {watchIntervalMin} minutes...[/]");
                        try { await Task.Delay(TimeSpan.FromMinutes(watchIntervalMin), cts.Token); }
                        catch (TaskCanceledException) { break; }
                    }
                }
                return 0;
            }
            else
            {
                var report = await orchestrator.RunAsync(mode, moduleName, cts.Token);
                var exporters = serviceProvider.GetServices<IExporter>();
                if (!System.IO.Directory.Exists(outputPath))
                {
                    System.IO.Directory.CreateDirectory(outputPath);
                }
                
                foreach (var exp in exporters)
                {
                    await exp.ExportAsync(report, outputPath);
                }
                var totalFindings = report.Results.Values.Sum(r => r.Findings.Count);
                return totalFindings > 0 ? 2 : 0;
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            if (!isSilent)
                ui.OnError("Application crashed.", ex);
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static string? GetArgValue(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return null;
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IConfiguration>(configuration);
        services.Configure<AppSettings>(configuration.GetSection("AppSettings"));

        services.AddSingleton<DatabaseContext>();
        services.AddSingleton<IScanRepository, ScanRepository>();
        
        services.AddTransient<ScannerFactory>();
        
        // Core Services
        services.AddTransient<LaptopForensics.Core.Interfaces.IWmiService, LaptopForensics.Core.Services.WmiService>();
        services.AddTransient<LaptopForensics.Core.Interfaces.IPowerShellService, LaptopForensics.Core.Services.PowerShellService>();
        services.AddTransient<LaptopForensics.Core.Interfaces.IRegistryService, LaptopForensics.Core.Services.RegistryService>();

        // Orchestrator
        services.AddTransient<ScanOrchestrator>();

        // Register Scanners
        services.AddTransient<IScanModule, HardwareScanner>();
        services.AddTransient<IScanModule, UserScanner>();
        services.AddTransient<IScanModule, NetworkScanner>();
        services.AddTransient<IScanModule, SoftwareScanner>();
        services.AddTransient<IScanModule, LicenseAuditor>();
        services.AddTransient<IScanModule, SecurityScanner>();
        services.AddTransient<IScanModule, StartupScanner>();
        services.AddTransient<IScanModule, BrowserScanner>();
        services.AddTransient<IScanModule, PerformanceScanner>();
        services.AddTransient<IScanModule, ThreatDetector>();
    }
}
