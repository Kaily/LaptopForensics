namespace LaptopForensics.Core.Interfaces;

/// <summary>
/// Defines persistence operations for scan history records.
/// </summary>
public interface IScanRepository
{
    /// <summary>
    /// Saves a scan history record.
    /// </summary>
    /// <param name="record">The scan history record to store.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveAsync(ScanHistory record);

    /// <summary>
    /// Gets all scan history records.
    /// </summary>
    /// <returns>All stored scan history records.</returns>
    Task<IEnumerable<ScanHistory>> GetAllAsync();

    /// <summary>
    /// Gets a scan history record by scan identifier.
    /// </summary>
    /// <param name="scanId">The scan identifier.</param>
    /// <returns>The matching record if found; otherwise <c>null</c>.</returns>
    Task<ScanHistory?> GetByIdAsync(string scanId);

    /// <summary>
    /// Gets the most recent scan history record.
    /// </summary>
    /// <returns>The latest record if one exists; otherwise <c>null</c>.</returns>
    Task<ScanHistory?> GetLatestAsync();

    /// <summary>
    /// Deletes records older than the specified cutoff time.
    /// </summary>
    /// <param name="cutoff">The cutoff timestamp.</param>
    /// <returns>A task representing the asynchronous delete operation.</returns>
    Task DeleteOlderThanAsync(DateTime cutoff);
}
