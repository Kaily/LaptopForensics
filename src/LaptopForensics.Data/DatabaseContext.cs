using Microsoft.Data.Sqlite;
using LaptopForensics.Core.Models;
using Microsoft.Extensions.Options;

namespace LaptopForensics.Data;

public sealed class DatabaseContext : IDisposable
{
    private readonly string _databasePath;
    private bool _disposed;

    public DatabaseContext(IOptions<AppSettings> settings)
    {
        _databasePath = string.IsNullOrWhiteSpace(settings.Value.Storage.DatabasePath)
            ? @"C:\LaptopForensics\Data\scans.db"
            : settings.Value.Storage.DatabasePath;
    }

    public void EnsureCreated()
    {
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var connection = GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS ScanHistory (
                Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                ScanId          TEXT    NOT NULL UNIQUE,
                ScanTimestamp   TEXT    NOT NULL,
                Hostname        TEXT    NOT NULL,
                OsVersion       TEXT,
                ScanMode        TEXT    NOT NULL,
                OverallScore    REAL,
                Grade           TEXT,
                ReportJson      TEXT,
                TotalDurationMs INTEGER,
                CreatedAt       TEXT    DEFAULT (datetime('now'))
            );

            CREATE INDEX IF NOT EXISTS idx_scan_timestamp ON ScanHistory(ScanTimestamp DESC);
            CREATE INDEX IF NOT EXISTS idx_hostname ON ScanHistory(Hostname);
            """;
        command.ExecuteNonQuery();
    }

    public SqliteConnection GetConnection()
    {
        var connection = new SqliteConnection($"Data Source={_databasePath};");
        connection.Open();
        return connection;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
    }
}
