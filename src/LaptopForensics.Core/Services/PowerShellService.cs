using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace LaptopForensics.Core.Services;

public class PowerShellService : IPowerShellService
{
    private readonly ILogger<PowerShellService> _logger;

    public PowerShellService(ILogger<PowerShellService> logger)
    {
        _logger = logger;
    }

    public async Task<string> RunAsync(string script, int timeoutSeconds = 10)
    {
        try
        {
            using var ps = PowerShell.Create();
            ps.AddScript(script);

            var output = await InvokeWithTimeoutAsync(ps, timeoutSeconds).ConfigureAwait(false);

            var builder = new StringBuilder();
            foreach (var item in output)
            {
                if (item is not null)
                {
                    builder.AppendLine(item.ToString());
                }
            }

            return builder.ToString().TrimEnd();
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "PowerShell script timed out after {TimeoutSeconds}s.", timeoutSeconds);
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PowerShell script execution failed.");
            return string.Empty;
        }
    }

    public async Task<IEnumerable<PSObject>> RunAndGetObjectsAsync(string script, int timeoutSeconds = 10)
    {
        try
        {
            using var ps = PowerShell.Create();
            ps.AddScript(script);

            var output = await InvokeWithTimeoutAsync(ps, timeoutSeconds).ConfigureAwait(false);
            return output.ToList();
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "PowerShell object script timed out after {TimeoutSeconds}s.", timeoutSeconds);
            return Array.Empty<PSObject>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PowerShell object script execution failed.");
            return Array.Empty<PSObject>();
        }
    }

    private static async Task<IReadOnlyCollection<PSObject>> InvokeWithTimeoutAsync(
        PowerShell ps,
        int timeoutSeconds)
    {
        var output = new PSDataCollection<PSObject>();
        var asyncResult = ps.BeginInvoke<PSObject, PSObject>(null, output);
        var completed = await Task.Run(
            () => asyncResult.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(timeoutSeconds)))
            .ConfigureAwait(false);

        if (!completed)
        {
            ps.Stop();
            throw new OperationCanceledException();
        }

        ps.EndInvoke(asyncResult);
        return output.ToList();
    }
}
