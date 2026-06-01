namespace LaptopForensics.Core.Models;

public class UserAccountInfo
{
    public List<LocalAccount> Accounts { get; set; } = new();
    public List<ActiveSession> ActiveSessions { get; set; } = new();
    public List<LoginEvent> LoginHistory { get; set; } = new();
    public PasswordPolicy PasswordPolicy { get; set; } = new();
    public int TotalAccounts => Accounts.Count;
    public int ActiveAccounts => Accounts.Count(a => a.IsEnabled);
    public int AdminAccounts => Accounts.Count(a => a.IsAdmin);
    public int InactiveAccounts => Accounts.Count(a => a.LastLogin < DateTime.Now.AddDays(-90));
}

public class LocalAccount
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Sid { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsBuiltIn { get; set; }
    public DateTime? LastLogin { get; set; }
    public DateTime? AccountCreated { get; set; }
    public DateTime? PasswordLastSet { get; set; }
    public bool PasswordNeverExpires { get; set; }
    public List<string> Groups { get; set; } = new();
    public string ProfilePath { get; set; } = string.Empty;
    public long ProfileSizeBytes { get; set; }
    public int FailedLoginCount30Days { get; set; }
}

public class ActiveSession
{
    public string Username { get; set; } = string.Empty;
    public int SessionId { get; set; }
    public string LogonType { get; set; } = string.Empty;
    public DateTime LoginTime { get; set; }
    public TimeSpan IdleTime { get; set; }
    public string? RemoteIp { get; set; }
}

public class LoginEvent
{
    public DateTime Timestamp { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string LogonType { get; set; } = string.Empty;
    public string? RemoteIp { get; set; }
    public int EventId { get; set; }
}

public class PasswordPolicy
{
    public int MinimumLength { get; set; }
    public bool ComplexityRequired { get; set; }
    public int MaxPasswordAgeDays { get; set; }
    public int LockoutThreshold { get; set; }
}
