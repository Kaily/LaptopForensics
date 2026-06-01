using System.Diagnostics;
using System.Security.Principal;

namespace LaptopForensics.Core.Services;

public class ElevationService
{
    public bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public bool RestartAsAdmin()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas",
                Arguments = string.Join(" ", Environment.GetCommandLineArgs().Skip(1))
            };

            Process.Start(startInfo);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string GetElevationWarning()
    {
        return IsRunningAsAdmin()
            ? string.Empty
            : "Administrator privileges are not enabled. Some forensic data may be incomplete.";
    }
}
