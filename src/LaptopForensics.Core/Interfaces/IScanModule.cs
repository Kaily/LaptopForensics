namespace LaptopForensics.Core.Interfaces;

/// <summary>
/// Represents a pluggable scan module that can execute a forensics check and return its result.
/// </summary>
public interface IScanModule
{
    /// <summary>
    /// Gets the unique module name used in orchestration and reporting.
    /// </summary>
    string ModuleName { get; }

    /// <summary>
    /// Gets the display icon associated with this module.
    /// </summary>
    string ModuleIcon { get; }

    /// <summary>
    /// Gets the estimated runtime in seconds for progress tracking.
    /// </summary>
    int EstimatedSeconds { get; }

    /// <summary>
    /// Gets the scan modes in which this module is applicable.
    /// </summary>
    ScanMode ApplicableModes { get; }

    /// <summary>
    /// Executes the scan module.
    /// </summary>
    /// <param name="ct">Cancellation token used to cancel execution.</param>
    /// <returns>The module result containing score, findings, and module-specific data.</returns>
    Task<ModuleResult> ExecuteAsync(CancellationToken ct);
}
