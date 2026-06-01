using System.Management.Automation;

namespace LaptopForensics.Core.Interfaces;

/// <summary>
/// Provides PowerShell execution helpers for scanner modules.
/// </summary>
public interface IPowerShellService
{
    /// <summary>
    /// Executes a PowerShell script and returns plain-text output.
    /// </summary>
    /// <param name="script">The PowerShell script to run.</param>
    /// <param name="timeoutSeconds">Execution timeout in seconds.</param>
    /// <returns>Script output as a single string.</returns>
    Task<string> RunAsync(string script, int timeoutSeconds = 10);

    /// <summary>
    /// Executes a PowerShell script and returns object output.
    /// </summary>
    /// <param name="script">The PowerShell script to run.</param>
    /// <param name="timeoutSeconds">Execution timeout in seconds.</param>
    /// <returns>Script output objects.</returns>
    Task<IEnumerable<PSObject>> RunAndGetObjectsAsync(string script, int timeoutSeconds = 10);
}
