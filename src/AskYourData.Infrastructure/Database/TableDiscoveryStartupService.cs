using AskYourData.Core.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AskYourData.Infrastructure.Database;

/// <summary>
/// Runs once at startup: discovers all user tables in each configured database
/// and populates ChatDatabase.Tables in memory — without running full vector ingestion.
/// This prevents the "no tables indexed yet" error on first request after a restart.
/// </summary>
public class TableDiscoveryStartupService : IHostedService
{
    private readonly List<ChatDatabase> _databases;
    private readonly ILogger<TableDiscoveryStartupService> _logger;

    public TableDiscoveryStartupService(
        List<ChatDatabase> databases,
        ILogger<TableDiscoveryStartupService> logger)
    {
        _databases = databases;
        _logger    = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var db in _databases)
        {
            if (db.Tables.Count > 0)
            {
                _logger.LogInformation(
                    "[Startup] {Db} already has {Count} tables — skipping discovery.",
                    db.Name, db.Tables.Count);
                continue;
            }

            _logger.LogInformation("[Startup] Discovering tables for database {Db}...", db.Name);
            try
            {
                var tableNames = await DiscoverTablesAsync(db.ConnectionString, cancellationToken);
                foreach (var name in tableNames)
                    db.Tables.Add(new ChatTable { TableName = name, Description = name });

                _logger.LogInformation(
                    "[Startup] {Db}: discovered {Count} tables.",
                    db.Name, tableNames.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[Startup] Failed to discover tables for {Db}. " +
                    "Chat will show 'no tables indexed' until /ingest is run.", db.Name);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task<List<string>> DiscoverTablesAsync(
        string connectionString, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT TABLE_SCHEMA + '.' + TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_SCHEMA, TABLE_NAME
            """;

        await using var cmd    = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var tables = new List<string>();
        while (await reader.ReadAsync(ct))
            tables.Add(reader.GetString(0));

        return tables;
    }
}
