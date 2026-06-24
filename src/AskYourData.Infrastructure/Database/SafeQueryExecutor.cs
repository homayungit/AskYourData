using System.Diagnostics;
using AskYourData.Core.Interfaces;
using AskYourData.Core.Models;
using AskYourData.Core.Options;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AskYourData.Infrastructure.Database;

/// <summary>
/// Validates SQL for blocked keywords, then executes it read-only with timeout and row limits.
/// </summary>
public class SafeQueryExecutor : ISafeQueryExecutor
{
    private static readonly string[] BlockedKeywords =
    [
        "INSERT", "UPDATE", "DELETE", "DROP", "TRUNCATE",
        "ALTER", "CREATE", "EXEC", "EXECUTE", "XP_", "SP_",
        "--", "/*"
    ];

    private readonly ChatbotOptions _options;
    private readonly ILogger<SafeQueryExecutor> _logger;

    public SafeQueryExecutor(IOptions<ChatbotOptions> options, ILogger<SafeQueryExecutor> logger)
    {
        _options = options.Value;
        _logger  = logger;
    }

    public async Task<QueryResult> ExecuteAsync(
        string sql,
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        // ── Security validation ────────────────────────────────────────────
        var upperSql = sql.ToUpperInvariant();
        foreach (var keyword in BlockedKeywords)
        {
            if (upperSql.Contains(keyword))
            {
                _logger.LogWarning("Blocked SQL containing '{Keyword}': {Sql}", keyword, sql);
                return new QueryResult
                {
                    Success      = false,
                    ErrorMessage = $"SQL blocked: contains forbidden keyword '{keyword}'."
                };
            }
        }

        if (!upperSql.TrimStart().StartsWith("SELECT"))
        {
            _logger.LogWarning("Blocked non-SELECT SQL: {Sql}", sql);
            return new QueryResult { Success = false, ErrorMessage = "Only SELECT statements are allowed." };
        }

        // ── Inject ApplicationIntent=ReadOnly ─────────────────────────────
        var csBuilder = new SqlConnectionStringBuilder(connectionString)
        {
            ApplicationIntent = ApplicationIntent.ReadOnly
        };

        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new SqlConnection(csBuilder.ConnectionString);
            await conn.OpenAsync(cancellationToken);

            await using var cmd = new SqlCommand(sql, conn)
            {
                CommandTimeout = _options.SqlTimeoutSeconds
            };

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            var rows   = new List<Dictionary<string, object>>();
            var maxRows = _options.MaxContextRows;

            while (await reader.ReadAsync(cancellationToken) && rows.Count < maxRows)
            {
                var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? string.Empty : reader.GetValue(i);
                rows.Add(row);
            }

            sw.Stop();
            _logger.LogInformation("Query returned {Rows} rows in {Ms}ms", rows.Count, sw.ElapsedMilliseconds);

            return new QueryResult
            {
                Success     = true,
                Rows        = rows,
                RowCount    = rows.Count,
                ExecutionMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Query execution failed after {Ms}ms: {Sql}", sw.ElapsedMilliseconds, sql);
            return new QueryResult
            {
                Success      = false,
                ErrorMessage = $"Query failed: {ex.Message}",
                ExecutionMs  = sw.ElapsedMilliseconds
            };
        }
    }
}
