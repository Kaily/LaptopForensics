using System.Diagnostics;
using System.Security.Principal;
using System.Text.RegularExpressions;

namespace LaptopForensics.Core.Scanners;

public class UserScanner : IScanModule
{
    private readonly IWmiService _wmi;
    private readonly IPowerShellService _ps;
    private readonly ILogger<UserScanner> _logger;

    public UserScanner(IWmiService wmi, IPowerShellService ps, ILogger<UserScanner> logger)
    {
        _wmi = wmi;
        _ps = ps;
        _logger = logger;
    }

    public string ModuleName => "UserScanner";
    public string ModuleIcon => "\ud83d\udc64";
    public int EstimatedSeconds => 10;
    public ScanMode ApplicableModes => ScanMode.Full | ScanMode.Quick;

    public async Task<ModuleResult> ExecuteAsync(CancellationToken ct)
    {
        var started = DateTime.UtcNow;
        var data = new UserAccountInfo();

        try
        {
            ct.ThrowIfCancellationRequested();
            ReadAccounts(data);
            ReadGroups(data);
            ReadLastLogins(data);
            ReadProfiles(data);
            ReadActiveSessions(data);
            ReadLoginHistory(data);
            ApplyFailedLoginCounts(data);
            data.PasswordPolicy = await ReadPasswordPolicyAsync().ConfigureAwait(false);

            var score = CalculateScore(data);
            var findings = BuildFindings(data);

            return new ModuleResult
            {
                ModuleName = ModuleName,
                Success = true,
                Score = score,
                Grade = Helpers.ScoreCalculator.GetGrade(score),
                Findings = findings,
                Data = data,
                DurationMs = ElapsedMs(started)
            };
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "User scan was cancelled.");
            return FailedResult(data, "Cancelled", started);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "User scan failed unexpectedly.");
            return FailedResult(data, "User module incomplete: account data unavailable", started);
        }
    }

    private void ReadAccounts(UserAccountInfo data)
    {
        try
        {
            foreach (var account in _wmi.Query("Win32_UserAccount", "LocalAccount=True"))
            {
                var name = GetString(account, "Name");
                var sid = GetString(account, "SID");

                data.Accounts.Add(new LocalAccount
                {
                    Name = name,
                    FullName = GetString(account, "FullName"),
                    Sid = sid,
                    IsEnabled = !GetBool(account, "Disabled"),
                    IsBuiltIn = sid.StartsWith("S-1-5-21", StringComparison.OrdinalIgnoreCase) &&
                                (sid.EndsWith("-500", StringComparison.OrdinalIgnoreCase) ||
                                 sid.EndsWith("-501", StringComparison.OrdinalIgnoreCase)),
                    PasswordNeverExpires = GetBool(account, "PasswordExpires") == false,
                    PasswordLastSet = GetBool(account, "PasswordRequired") ? null : DateTime.MinValue,
                    ProfilePath = Path.Combine(@"C:\Users", name)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read local user accounts.");
        }
    }

    private void ReadGroups(UserAccountInfo data)
    {
        try
        {
            var groupLinks = _wmi.Query("Win32_GroupUser").ToList();

            foreach (var account in data.Accounts)
            {
                foreach (var link in groupLinks)
                {
                    var partComponent = GetString(link, "PartComponent");
                    var groupComponent = GetString(link, "GroupComponent");
                    if (!partComponent.Contains($"Name=\"{account.Name}\"", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var groupName = ExtractWmiName(groupComponent);
                    if (!string.IsNullOrWhiteSpace(groupName) && !account.Groups.Contains(groupName))
                    {
                        account.Groups.Add(groupName);
                    }
                }

                account.IsAdmin = account.Groups.Any(g => g.Equals("Administrators", StringComparison.OrdinalIgnoreCase));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read local group memberships.");
        }
    }

    private void ReadLastLogins(UserAccountInfo data)
    {
        try
        {
            var profiles = _wmi.Query("Win32_NetworkLoginProfile").ToList();
            foreach (var account in data.Accounts)
            {
                var profile = profiles.FirstOrDefault(p =>
                    GetString(p, "Name").EndsWith($"\\{account.Name}", StringComparison.OrdinalIgnoreCase) ||
                    GetString(p, "Name").Equals(account.Name, StringComparison.OrdinalIgnoreCase));

                if (profile is null)
                {
                    continue;
                }

                account.LastLogin = Helpers.DateTimeHelper.ParseWmiDate(GetString(profile, "LastLogon"));
                var passwordAgeSeconds = GetLong(profile, "PasswordAge");
                if (passwordAgeSeconds > 0)
                {
                    account.PasswordLastSet = DateTime.Now.AddSeconds(-passwordAgeSeconds);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read user login profile information.");
        }
    }

    private void ReadProfiles(UserAccountInfo data)
    {
        foreach (var account in data.Accounts)
        {
            try
            {
                if (!Directory.Exists(account.ProfilePath))
                {
                    continue;
                }

                account.ProfileSizeBytes = GetDirectorySize(account.ProfilePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read profile size for {User}.", account.Name);
            }
        }
    }

    private void ReadActiveSessions(UserAccountInfo data)
    {
        try
        {
            foreach (var session in _wmi.Query("Win32_LogonSession"))
            {
                var logonId = GetString(session, "LogonId");
                var users = _wmi.Query(
                    "Win32_LoggedOnUser",
                    $"Dependent LIKE '%LogonId=\"{logonId}\"%'").ToList();

                foreach (var user in users)
                {
                    var antecedent = GetString(user, "Antecedent");
                    var username = ExtractWmiName(antecedent);
                    if (string.IsNullOrWhiteSpace(username))
                    {
                        continue;
                    }

                    data.ActiveSessions.Add(new ActiveSession
                    {
                        Username = username,
                        SessionId = ParseInt(logonId),
                        LogonType = MapLogonType(GetInt(session, "LogonType")),
                        LoginTime = Helpers.DateTimeHelper.ParseWmiDate(GetString(session, "StartTime")) ?? DateTime.MinValue
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read active sessions.");
        }
    }

    private void ReadLoginHistory(UserAccountInfo data)
    {
        try
        {
            using var log = new EventLog("Security");
            var entries = log.Entries
                .Cast<EventLogEntry>()
                .Where(e => e.InstanceId is 4624 or 4625)
                .OrderByDescending(e => e.TimeGenerated)
                .Take(50);

            foreach (var entry in entries)
            {
                data.LoginHistory.Add(new LoginEvent
                {
                    Timestamp = entry.TimeGenerated,
                    Username = ExtractEventUsername(entry),
                    Success = entry.InstanceId == 4624,
                    LogonType = ExtractEventLogonType(entry),
                    RemoteIp = ExtractRemoteIp(entry),
                    EventId = (int)entry.InstanceId
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read Security event log.");
        }
    }

    private void ApplyFailedLoginCounts(UserAccountInfo data)
    {
        var cutoff = DateTime.Now.AddDays(-30);
        foreach (var account in data.Accounts)
        {
            account.FailedLoginCount30Days = data.LoginHistory.Count(e =>
                !e.Success &&
                e.Timestamp >= cutoff &&
                e.Username.Equals(account.Name, StringComparison.OrdinalIgnoreCase));
        }
    }

    private async Task<PasswordPolicy> ReadPasswordPolicyAsync()
    {
        var policy = new PasswordPolicy();

        try
        {
            var output = await _ps.RunAsync("net accounts", timeoutSeconds: 10).ConfigureAwait(false);
            foreach (var line in output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Contains("Minimum password length", StringComparison.OrdinalIgnoreCase))
                {
                    policy.MinimumLength = ExtractLastInt(line);
                }
                else if (line.Contains("Maximum password age", StringComparison.OrdinalIgnoreCase))
                {
                    policy.MaxPasswordAgeDays = ExtractLastInt(line);
                }
                else if (line.Contains("Lockout threshold", StringComparison.OrdinalIgnoreCase))
                {
                    policy.LockoutThreshold = ExtractLastInt(line);
                }
            }

            policy.ComplexityRequired = policy.MinimumLength >= 8;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read password policy.");
        }

        return policy;
    }

    private static double CalculateScore(UserAccountInfo data)
    {
        var score = 100d;

        if (data.Accounts.Any(a => a.IsEnabled && a.PasswordLastSet == DateTime.MinValue))
        {
            score -= 25;
        }

        if (data.Accounts.Any(a => a.IsEnabled && a.LastLogin < DateTime.Now.AddDays(-90)))
        {
            score -= 20;
        }

        if (data.Accounts.Any(a => a.IsAdmin && a.PasswordLastSet == DateTime.MinValue))
        {
            score -= 15;
        }

        if (data.Accounts.Any(a => a.IsEnabled && a.Name.Equals("Guest", StringComparison.OrdinalIgnoreCase)))
        {
            score -= 15;
        }

        if (data.Accounts.Any(a => a.FailedLoginCount30Days >= 3))
        {
            score -= 10;
        }

        if (data.Accounts.Any(a => a.IsEnabled && a.Sid.EndsWith("-500", StringComparison.OrdinalIgnoreCase)))
        {
            score -= 10;
        }

        if (!data.PasswordPolicy.ComplexityRequired)
        {
            score -= 5;
        }

        if (data.PasswordPolicy.MinimumLength < 8)
        {
            score -= 5;
        }

        return Math.Clamp(score, 0, 100);
    }

    private static List<Finding> BuildFindings(UserAccountInfo data)
    {
        var findings = new List<Finding>();

        if (data.Accounts.Any(a => a.IsEnabled && a.Name.Equals("Guest", StringComparison.OrdinalIgnoreCase)))
        {
            var evidence = new Dictionary<string, string> { { "Account", "Guest" }, { "Status", "Enabled" }, { "Action", "net user Guest /active:no" } };
            findings.Add(CreateFinding(Severity.Critical, "Guest account is a security risk", "The Guest account is enabled.", "Disable the Guest account.", ConfidenceLevel.High, evidence));
        }

        foreach (var account in data.Accounts.Where(a => a.FailedLoginCount30Days >= 10))
        {
            var evidence = new Dictionary<string, string> { { "Account", account.Name }, { "Failed Logins (30d)", account.FailedLoginCount30Days.ToString() }, { "Action", "Review Security Event Logs (Event ID 4625)." } };
            findings.Add(CreateFinding(Severity.Critical, "Possible brute force attempt", $"{account.Name} has {account.FailedLoginCount30Days} failed logins in 30 days.", "Review Security log events and reset the password if needed.", ConfidenceLevel.High, evidence));
        }

        foreach (var account in data.Accounts.Where(a => a.IsEnabled && a.LastLogin < DateTime.Now.AddDays(-90)))
        {
            var evidence = new Dictionary<string, string> { { "Account", account.Name }, { "Last Login", account.LastLogin?.ToString("yyyy-MM-dd") ?? "Never" }, { "Action", $"net user {account.Name} /active:no" } };
            findings.Add(CreateFinding(Severity.Warning, $"Inactive account: {account.Name}", "Enabled account has not logged in for 90+ days.", "Disable or remove stale accounts.", ConfidenceLevel.Medium, evidence));
        }

        foreach (var account in data.Accounts.Where(a => a.PasswordNeverExpires))
        {
            var evidence = new Dictionary<string, string> { { "Account", account.Name }, { "Setting", "PasswordNeverExpires=True" }, { "Action", $"wmic useraccount where name='{account.Name}' set PasswordExpires=True" } };
            findings.Add(CreateFinding(Severity.Warning, $"Account {account.Name} has no password expiry", "Password expiry is disabled for this account.", "Enable normal password rotation unless there is an approved exception.", ConfidenceLevel.High, evidence));
        }

        if (data.Accounts.Any(a => a.IsEnabled && a.Sid.EndsWith("-500", StringComparison.OrdinalIgnoreCase)))
        {
            var admin = data.Accounts.First(a => a.Sid.EndsWith("-500", StringComparison.OrdinalIgnoreCase));
            var evidence = new Dictionary<string, string> { { "Account", admin.Name }, { "SID", admin.Sid }, { "Status", "Enabled" }, { "Action", $"net user {admin.Name} /active:no" } };
            findings.Add(CreateFinding(Severity.Warning, "Built-in Administrator account is active", "The built-in Administrator account is enabled.", "Disable it or strictly restrict its use.", ConfidenceLevel.High, evidence));
        }

        if (data.AdminAccounts > 2)
        {
            var admins = data.Accounts.Where(a => a.IsAdmin).Select(a => a.Name).ToList();
            var evidence = new Dictionary<string, string> { { "Admin Count", data.AdminAccounts.ToString() }, { "Accounts", string.Join(", ", admins) }, { "Action", "Remove unnecessary users from Local Administrators group." } };
            findings.Add(CreateFinding(Severity.Info, "Multiple admin accounts detected", $"{data.AdminAccounts} local admin accounts were found.", "Review local administrator membership.", ConfidenceLevel.Medium, evidence));
        }

        foreach (var account in data.Accounts.Where(a => a.ProfileSizeBytes > 0 && a.LastLogin < DateTime.Now.AddDays(-90)))
        {
            var evidence = new Dictionary<string, string> { { "Account", account.Name }, { "Profile Size", Helpers.SizeFormatter.FormatBytes(account.ProfileSizeBytes) }, { "Last Login", account.LastLogin?.ToString("yyyy-MM-dd") ?? "Never" }, { "Action", "Delete profile via Advanced System Settings -> User Profiles." } };
            findings.Add(CreateFinding(Severity.Info, $"Unused profile: {account.Name}, {Helpers.SizeFormatter.FormatBytes(account.ProfileSizeBytes)}", "Old user profile data exists on disk.", "Archive or remove unused profile data if no longer required.", ConfidenceLevel.Medium, evidence));
        }

        return findings;
    }

    private static Finding CreateFinding(Severity severity, string title, string description, string recommendation, ConfidenceLevel confidence = ConfidenceLevel.Medium, Dictionary<string, string>? evidence = null)
    {
        return new Finding
        {
            Level = severity,
            Title = title,
            Description = description,
            Recommendation = recommendation,
            Confidence = confidence,
            Evidence = evidence ?? new Dictionary<string, string>()
        };
    }

    private static ModuleResult FailedResult(UserAccountInfo data, string message, DateTime started)
    {
        return new ModuleResult
        {
            ModuleName = "UserScanner",
            Success = false,
            Score = 0,
            Grade = "POOR",
            Findings = new List<Finding>
            {
                CreateFinding(Severity.Warning, "User scan incomplete", message, "Run the application as administrator and verify WMI/Event Log access.", ConfidenceLevel.Low, new Dictionary<string, string> { { "Error", message } })
            },
            Data = data,
            DurationMs = ElapsedMs(started),
            ErrorMessage = message
        };
    }

    private static long GetDirectorySize(string path)
    {
        long total = 0;

        try
        {
            foreach (var file in Directory.EnumerateFiles(path))
            {
                try
                {
                    total += new FileInfo(file).Length;
                }
                catch (UnauthorizedAccessException)
                {
                }
                catch (IOException)
                {
                }
            }

            foreach (var directory in Directory.EnumerateDirectories(path))
            {
                total += GetDirectorySize(directory);
            }
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (IOException)
        {
        }

        return total;
    }

    private static string ExtractWmiName(string wmiPath)
    {
        var match = Regex.Match(wmiPath, "Name=\"(?<name>[^\"]+)\"");
        return match.Success ? match.Groups["name"].Value : string.Empty;
    }

    private static string ExtractEventUsername(EventLogEntry entry)
    {
        try
        {
            var replacements = entry.ReplacementStrings;
            return replacements.Length > 5 ? replacements[5] : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ExtractEventLogonType(EventLogEntry entry)
    {
        try
        {
            var replacements = entry.ReplacementStrings;
            return replacements.Length > 8 ? MapLogonType(ParseInt(replacements[8])) : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string? ExtractRemoteIp(EventLogEntry entry)
    {
        try
        {
            return entry.ReplacementStrings.FirstOrDefault(s => Regex.IsMatch(s, @"^\d{1,3}(\.\d{1,3}){3}$"));
        }
        catch
        {
            return null;
        }
    }

    private static string MapLogonType(int type)
    {
        return type switch
        {
            2 => "Interactive",
            3 => "Network",
            4 => "Batch",
            5 => "Service",
            7 => "Unlock",
            10 => "RemoteInteractive",
            11 => "CachedInteractive",
            _ => type > 0 ? $"Type {type}" : string.Empty
        };
    }

    private static string GetString(Dictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || value is null)
        {
            return string.Empty;
        }

        return Convert.ToString(value) ?? string.Empty;
    }

    private static int GetInt(Dictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || value is null)
        {
            return 0;
        }

        return ParseInt(Convert.ToString(value));
    }

    private static bool GetBool(Dictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || value is null)
        {
            return false;
        }

        try
        {
            return Convert.ToBoolean(value);
        }
        catch
        {
            return false;
        }
    }

    private static long GetLong(Dictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || value is null)
        {
            return 0;
        }

        try
        {
            return Convert.ToInt64(value);
        }
        catch
        {
            return 0;
        }
    }

    private static int ParseInt(string? value)
    {
        return int.TryParse(value, out var parsed) ? parsed : 0;
    }

    private static int ExtractLastInt(string line)
    {
        var matches = Regex.Matches(line, @"\d+");
        return matches.Count == 0 ? 0 : ParseInt(matches[^1].Value);
    }

    private static long ElapsedMs(DateTime started)
    {
        return (long)(DateTime.UtcNow - started).TotalMilliseconds;
    }
}
