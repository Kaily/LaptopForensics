namespace LaptopForensics.Core.Interfaces;

/// <summary>
/// Receives scan lifecycle notifications for UI and reporting updates.
/// </summary>
public interface IScanProgressObserver
{
    /// <summary>
    /// Called when a module starts execution.
    /// </summary>
    /// <param name="moduleName">The module name.</param>
    /// <param name="icon">The module display icon.</param>
    /// <param name="estimatedSeconds">Estimated module duration in seconds.</param>
    void OnModuleStarted(string moduleName, string icon, int estimatedSeconds);

    /// <summary>
    /// Called when a module completes execution.
    /// </summary>
    /// <param name="moduleName">The module name.</param>
    /// <param name="result">The module result.</param>
    void OnModuleCompleted(string moduleName, ModuleResult result);

    /// <summary>
    /// Called when the full scan completes.
    /// </summary>
    /// <param name="report">The final scan report.</param>
    void OnScanCompleted(ScanReport report);
}
