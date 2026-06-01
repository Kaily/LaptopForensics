namespace LaptopForensics.Core.Interfaces;

/// <summary>
/// Defines an exporter that writes a scan report to a specific output format.
/// </summary>
public interface IExporter
{
    /// <summary>
    /// Gets the display name of the export format.
    /// </summary>
    string FormatName { get; }

    /// <summary>
    /// Gets the file extension used by this exporter.
    /// </summary>
    string FileExtension { get; }

    /// <summary>
    /// Exports a scan report to the specified output path.
    /// </summary>
    /// <param name="report">The scan report to export.</param>
    /// <param name="outputPath">The destination file or directory path.</param>
    /// <returns>A task representing the asynchronous export operation.</returns>
    Task ExportAsync(ScanReport report, string outputPath);
}
