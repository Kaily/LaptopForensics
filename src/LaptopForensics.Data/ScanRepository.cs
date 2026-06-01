using Microsoft.Data.Sqlite;
using LaptopForensics.Core.Interfaces;
using LaptopForensics.Core.Models;
using Microsoft.Extensions.Logging;

namespace LaptopForensics.Data;

public class ScanRepository : IScanRepository
{
    private readonly DatabaseContext _context;
    private readonly ILogger<ScanRepository> _logger;

    public ScanRepository(DatabaseContext context, ILogger<ScanRepository> logger)
    {
        _context = context;
        _logger = logger;
        _context.EnsureCreated();
    }

    public async Task SaveAsync(ScanHistory record)
    {
        try
        {
            using var connection = _context.GetConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO ScanHistory (
                    ScanId,
                    ScanTimestamp,
                    Hostname,
                    OsVersion,
                    ScanMode,
                    OverallScore,
                    Grade,
                    ReportJson,
                    TotalDurationMs
                )
                VALUES (
                    @scanId,
                    @scanTimestamp,
                    @hostname,
                    @osVersion,
                    @scanMode,
                    @overallScore,
                    @grade,
                    @reportJson,
                    @totalDurationMs
                );
                """;

            AddParameter(command, "@scanId", record.ScanId);
            AddParameter(command, "@scanTimestamp", record.ScanTimestamp.ToString("O"));
            AddParameter(command, "@hostname", record.Hostname);
            AddParameter(command, "@osVersion", record.OsVersion);
            AddParameter(command, "@scanMode", record.ScanMode);
            AddParameter(command, "@overallScore", record.OverallScore);
            AddParameter(command, "@grade", record.Grade);
            AddParameter(command, "@reportJson", record.ReportJson);
            AddParameter(command, "@totalDurationMs", record.TotalDurationMs);

            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save scan history record {ScanId}.", record.ScanId);
            throw new ApplicationException("Failed to save scan history record.", ex);
        }
    }

    public async Task<IEnumerable<ScanHistory>> GetAllAsync()
    {
        try
        {
            using var connection = _context.GetConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM ScanHistory ORDER BY ScanTimestamp DESC;";
            return await ReadManyAsync(command).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read scan history records.");
            throw new ApplicationException("Failed to read scan history records.", ex);
        }
    }

    public async Task<ScanHistory?> GetByIdAsync(string scanId)
    {
        try
        {
            using var connection = _context.GetConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM ScanHistory WHERE ScanId = @scanId;";
            AddParameter(command, "@scanId", scanId);
            return (await ReadManyAsync(command).ConfigureAwait(false)).FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read scan history record {ScanId}.", scanId);
            throw new ApplicationException("Failed to read scan history record.", ex);
        }
    }

    public async Task<ScanHistory?> GetLatestAsync()
    {
        try
        {
            using var connection = _context.GetConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM ScanHistory ORDER BY ScanTimestamp DESC LIMIT 1;";
            return (await ReadManyAsync(command).ConfigureAwait(false)).FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read latest scan history record.");
            throw new ApplicationException("Failed to read latest scan history record.", ex);
        }
    }

    public async Task DeleteOlderThanAsync(DateTime cutoff)
    {
        try
        {
            using var connection = _context.GetConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM ScanHistory WHERE ScanTimestamp < @cutoff;";
            AddParameter(command, "@cutoff", cutoff.ToString("O"));
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete scan history records older than {Cutoff}.", cutoff);
            throw new ApplicationException("Failed to delete old scan history records.", ex);
        }
    }

    private static async Task<List<ScanHistory>> ReadManyAsync(SqliteCommand command)
    {
        var records = new List<ScanHistory>();
        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            records.Add(new ScanHistory
            {
                Id = Convert.ToInt32(reader["Id"]),
                ScanId = Convert.ToString(reader["ScanId"]) ?? string.Empty,
                ScanTimestamp = ParseDate(reader["ScanTimestamp"]),
                Hostname = Convert.ToString(reader["Hostname"]) ?? string.Empty,
                OsVersion = Convert.ToString(reader["OsVersion"]) ?? string.Empty,
                ScanMode = Convert.ToString(reader["ScanMode"]) ?? string.Empty,
                OverallScore = Convert.ToDouble(reader["OverallScore"]),
                Grade = Convert.ToString(reader["Grade"]) ?? string.Empty,
                ReportJson = Convert.ToString(reader["ReportJson"]) ?? string.Empty,
                TotalDurationMs = Convert.ToInt64(reader["TotalDurationMs"]),
                CreatedAt = ParseDate(reader["CreatedAt"])
            });
        }

        return records;
    }

    private static DateTime ParseDate(object value)
    {
        var raw = Convert.ToString(value);
        return DateTime.TryParse(raw, out var parsed) ? parsed : DateTime.MinValue;
    }

    private static void AddParameter(SqliteCommand command, string name, object? value)
    {
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }
}
