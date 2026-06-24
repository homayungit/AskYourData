using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AskYourData.Infrastructure.Database;

// ── Records for expert schema inspection ──────────────────────────────────
public record TableSummary(string SchemaName, string TableName, long ApproxRows);

public record ColumnDetail(
    string ColumnName,
    string DataType,
    bool   IsNullable,
    int?   MaxLength,
    int?   NumericPrecision,
    int?   NumericScale,
    bool   IsAuditable);

/// <summary>
/// Reads INFORMATION_SCHEMA.COLUMNS and sys.extended_properties from SQL Server
/// and caches the formatted schema string for 10 minutes.
/// Also exposes expert-only methods for full schema inspection.
/// </summary>
public class SchemaInspector
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<SchemaInspector> _logger;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public SchemaInspector(IMemoryCache cache, ILogger<SchemaInspector> logger)
    {
        _cache  = cache;
        _logger = logger;
    }

    public async Task<string> GetSchemaAsync(
        string connectionString,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        string cacheKey = $"schema:{connectionString.GetHashCode()}:{tableName}";

        if (_cache.TryGetValue(cacheKey, out string? cached) && cached is not null)
        {
            _logger.LogDebug("Schema cache HIT for table {Table}", tableName);
            return cached;
        }

        _logger.LogInformation("Loading schema for table {Table}", tableName);

        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken);

            var columns = await GetColumnsAsync(conn, tableName, cancellationToken);
            var descriptions = await GetExtendedPropertiesAsync(conn, tableName, cancellationToken);

            var lines = new List<string>
            {
                $"Table: {tableName}",
                "Columns:"
            };

            foreach (var col in columns)
            {
                string desc = descriptions.TryGetValue(col.ColumnName, out var d) ? $" -- {d}" : string.Empty;
                lines.Add($"  - {col.ColumnName} ({col.DataType}){desc}");
            }

            string schema = string.Join(Environment.NewLine, lines);
            _cache.Set(cacheKey, schema, CacheDuration);
            return schema;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load schema for table {Table}", tableName);
            return $"Table: {tableName}\n(Schema unavailable: {ex.Message})";
        }
    }

    private static async Task<List<(string ColumnName, string DataType)>> GetColumnsAsync(
        SqlConnection conn, string tableName, CancellationToken ct)
    {
        const string sql = """
            SELECT COLUMN_NAME, DATA_TYPE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = @TableName
            ORDER BY ORDINAL_POSITION
            """;

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@TableName", tableName);

        var result = new List<(string, string)>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add((reader.GetString(0), reader.GetString(1)));

        return result;
    }

    private static async Task<Dictionary<string, string>> GetExtendedPropertiesAsync(
        SqlConnection conn, string tableName, CancellationToken ct)
    {
        const string sql = """
            SELECT c.name AS ColumnName, CAST(ep.value AS NVARCHAR(500)) AS Description
            FROM sys.columns c
            INNER JOIN sys.tables t  ON c.object_id = t.object_id
            INNER JOIN sys.extended_properties ep
                ON ep.major_id = c.object_id
               AND ep.minor_id = c.column_id
               AND ep.name = 'MS_Description'
            WHERE t.name = @TableName
            """;

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@TableName", tableName);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                result[reader.GetString(0)] = reader.GetString(1);
        }
        catch
        {
            // Extended properties may not exist — ignore silently
        }
        return result;
    }

    // ── Expert schema inspection (backend-only, not exposed via frontend) ──

    /// <summary>Returns all base tables with approximate row counts.</summary>
    public async Task<IReadOnlyList<TableSummary>> GetTablesAsync(
        string connectionString, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT t.TABLE_SCHEMA, t.TABLE_NAME,
                   CAST(ISNULL(SUM(p.rows), 0) AS BIGINT) AS ApproxRows
            FROM INFORMATION_SCHEMA.TABLES t
            LEFT JOIN sys.schemas  sc ON sc.name = t.TABLE_SCHEMA
            LEFT JOIN sys.tables   st ON st.name = t.TABLE_NAME
                                     AND st.schema_id = sc.schema_id
            LEFT JOIN sys.partitions p ON p.object_id = st.object_id
                                      AND p.index_id IN (0, 1)
            WHERE t.TABLE_TYPE = 'BASE TABLE'
            GROUP BY t.TABLE_SCHEMA, t.TABLE_NAME
            ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME
            """;

        var result = new List<TableSummary>();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new TableSummary(reader.GetString(0), reader.GetString(1), reader.GetInt64(2)));
        return result;
    }

    /// <summary>
    /// Returns all columns for a table with full type details and auditable flag.
    /// Auditable columns: Created*/Modified*/Updated*/Deleted* By/On/Date/Time patterns.
    /// </summary>
    public async Task<IReadOnlyList<ColumnDetail>> GetColumnsDetailedAsync(
        string connectionString, string tableName, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE,
                   CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = @TableName
            ORDER BY ORDINAL_POSITION
            """;

        var result = new List<ColumnDetail>();
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };
        cmd.Parameters.AddWithValue("@TableName", tableName);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var col = reader.GetString(0);
            result.Add(new ColumnDetail(
                col,
                reader.GetString(1),
                reader.GetString(2) == "YES",
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                reader.IsDBNull(4) ? null : (int)reader.GetByte(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5),
                IsAuditColumn(col)));
        }
        return result;
    }

    private static readonly string[] AuditPrefixes =
        ["created", "modified", "updated", "deleted", "inserted", "lastmodified"];

    private static bool IsAuditColumn(string columnName)
    {
        var lower = columnName.ToLowerInvariant();
        // Columns ending with audit-related suffixes AND starting with an audit prefix
        if (lower.EndsWith("by")   || lower.EndsWith("date") ||
            lower.EndsWith("time") || lower.EndsWith("on"))
        {
            foreach (var prefix in AuditPrefixes)
                if (lower.StartsWith(prefix)) return true;
        }
        // AD login tracking columns
        if (lower.EndsWith("adloginid")) return true;
        // Explicit known names
        return lower is "rowversion" or "timestamp" or "rowstamp"
                     or "lastmodified" or "lastmodifieddate" or "lastmodifiedby";
    }
}
