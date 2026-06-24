using AskYourData.Core.Models;

namespace AskYourData.Core.Interfaces;

public interface ISafeQueryExecutor
{
    /// <summary>
    /// Validates and executes a SQL SELECT query against the given connection string.
    /// Blocks any non-SELECT or dangerous SQL before execution.
    /// </summary>
    Task<QueryResult> ExecuteAsync(string sql, string connectionString, CancellationToken cancellationToken = default);
}
