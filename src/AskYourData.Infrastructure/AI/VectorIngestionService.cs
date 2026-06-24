using AskYourData.Core.Interfaces;
using AskYourData.Core.Models;
using AskYourData.Core.Options;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Qdrant.Client;
using Qdrant.Client.Grpc;

#pragma warning disable SKEXP0001

namespace AskYourData.Infrastructure.AI;

/// <summary>
/// Ingests ERP table rows as text embeddings into Qdrant.
/// Supports incremental sync via an in-memory watermark per table.
/// </summary>
public class VectorIngestionService : IVectorIngestionService
{
    private readonly SemanticKernelFactory _kernelFactory;
    private readonly List<ChatDatabase> _databases;
    private readonly QdrantOptions _qdrantOpts;
    private readonly ChatbotOptions _chatbotOpts;
    private readonly ILogger<VectorIngestionService> _logger;

    // In-memory watermark: key = "dbName:tableName"
    private readonly Dictionary<string, DateTime> _watermarks = new();

    private QdrantClient? _qdrantClient;

    public VectorIngestionService(
        SemanticKernelFactory kernelFactory,
        List<ChatDatabase> databases,
        IOptions<QdrantOptions> qdrantOpts,
        IOptions<ChatbotOptions> chatbotOpts,
        ILogger<VectorIngestionService> logger)
    {
        _kernelFactory = kernelFactory;
        _databases     = databases;
        _qdrantOpts    = qdrantOpts.Value;
        _chatbotOpts   = chatbotOpts.Value;
        _logger        = logger;
    }

    private QdrantClient GetQdrantClient() =>
        _qdrantClient ??= new QdrantClient(_qdrantOpts.Host, _qdrantOpts.Port);

    // ── Ensure collection exists ────────────────────────────────────────────
    private async Task EnsureCollectionAsync(IEmbeddingGenerator<string, Embedding<float>> embeddingService, CancellationToken ct)
    {
        var client = GetQdrantClient();
        var collections = await client.ListCollectionsAsync(ct);

        if (collections.Any(c => c == _qdrantOpts.CollectionName))
            return;

        var sampleResults = await embeddingService.GenerateAsync(new[] { "test" }, cancellationToken: ct);
        ulong vectorSize = (ulong)sampleResults[0].Vector.Length;

        await client.CreateCollectionAsync(
            _qdrantOpts.CollectionName,
            new VectorParams { Size = vectorSize, Distance = Distance.Cosine },
            cancellationToken: ct);

        _logger.LogInformation("Created Qdrant collection '{Name}' with dim={Dim}",
            _qdrantOpts.CollectionName, vectorSize);
    }

    // ── Ingest a single table ────────────────────────────────────────────────
    public async Task IngestTableAsync(
        string dbName, string tableName, string connectionString,
        CancellationToken cancellationToken = default)
    {
        var kernel           = await _kernelFactory.GetKernelAsync(cancellationToken);
        var embeddingService = kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        await EnsureCollectionAsync(embeddingService, cancellationToken);

        string watermarkKey = $"{dbName}:{tableName}";
        _watermarks.TryGetValue(watermarkKey, out var lastIngest);

        _logger.LogInformation("Ingesting {Db}.{Table} (since {Since})", dbName, tableName, lastIngest);

        var rows = await FetchRowsAsync(connectionString, tableName, lastIngest, cancellationToken);
        if (rows.Count == 0)
        {
            _logger.LogInformation("No new rows for {Db}.{Table}", dbName, tableName);
            return;
        }

        var client   = GetQdrantClient();
        var points   = new List<PointStruct>();

        foreach (var row in rows)
        {
            string text    = SerializeRow(dbName, tableName, row);
            var    embResults = await embeddingService.GenerateAsync(new[] { text }, cancellationToken: cancellationToken);
            float[] vectors = embResults[0].Vector.ToArray();
            string rowKey  = string.Join("_", row.Take(2).Select(kv => kv.Value?.ToString() ?? ""));

            var point = new PointStruct
            {
                Id      = new PointId { Uuid = Guid.NewGuid().ToString() },
                Vectors = vectors
            };
            point.Payload["dbName"]    = dbName;
            point.Payload["tableName"] = tableName;
            point.Payload["rowKey"]    = rowKey;
            point.Payload["content"]   = text;
            point.Payload["ingestedAt"]= DateTime.UtcNow.ToString("O");

            points.Add(point);
        }

        await client.UpsertAsync(_qdrantOpts.CollectionName, points, cancellationToken: cancellationToken);
        _watermarks[watermarkKey] = DateTime.UtcNow;

        _logger.LogInformation("Upserted {Count} vectors for {Db}.{Table}", points.Count, dbName, tableName);
    }

    // ── Ingest all databases ─────────────────────────────────────────────────
    public async Task IngestAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var db in _databases)
        {
            // Auto-discover tables if none configured
            if (db.Tables.Count == 0)
            {
                _logger.LogInformation("Auto-discovering tables for {Db}...", db.Name);
                var discovered = await DiscoverTablesAsync(db.ConnectionString, cancellationToken);
                foreach (var t in discovered)
                    db.Tables.Add(new ChatTable { TableName = t, Description = t });
                _logger.LogInformation("Discovered {Count} tables in {Db}: {Tables}",
                    discovered.Count, db.Name, string.Join(", ", discovered));
            }

            foreach (var table in db.Tables)
                await IngestTableAsync(db.Name, table.TableName, db.ConnectionString, cancellationToken);
        }
    }

    // ── Discover all user tables in a database ───────────────────────────────
    private static async Task<List<string>> DiscoverTablesAsync(string connectionString, CancellationToken ct)
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

    // ── Vector search ─────────────────────────────────────────────────────────
    public async Task<List<string>> SearchAsync(
        string question, int topK = 5, CancellationToken cancellationToken = default)
    {
        var kernel           = await _kernelFactory.GetKernelAsync(cancellationToken);
        var embeddingService = kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        var queryResults = await embeddingService.GenerateAsync(new[] { question }, cancellationToken: cancellationToken);
        float[] queryVector = queryResults[0].Vector.ToArray();
        var client      = GetQdrantClient();

        var results = await client.SearchAsync(
            _qdrantOpts.CollectionName,
            queryVector,
            limit: (ulong)topK,
            cancellationToken: cancellationToken);

        return results
            .Where(r => r.Payload.ContainsKey("content"))
            .Select(r => r.Payload["content"].StringValue)
            .ToList();
    }

    // ── Filtered vector search (by table names) ───────────────────────────────
    public async Task<List<string>> SearchFilteredAsync(
        string question,
        IReadOnlyList<string> tableNames,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        if (tableNames.Count == 0)
            return await SearchAsync(question, topK, cancellationToken);

        var kernel           = await _kernelFactory.GetKernelAsync(cancellationToken);
        var embeddingService = kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        var queryResults = await embeddingService.GenerateAsync(new[] { question }, cancellationToken: cancellationToken);
        float[] queryVector = queryResults[0].Vector.ToArray();
        var client = GetQdrantClient();

        // Build OR filter: tableName in [table1, table2, ...]
        var filter = new Filter();
        foreach (var table in tableNames)
        {
            filter.Should.Add(new Condition
            {
                Field = new FieldCondition
                {
                    Key   = "tableName",
                    Match = new Match { Keyword = table }
                }
            });
        }

        var results = await client.SearchAsync(
            _qdrantOpts.CollectionName,
            queryVector,
            filter: filter,
            limit: (ulong)topK,
            cancellationToken: cancellationToken);

        return results
            .Where(r => r.Payload.ContainsKey("content"))
            .Select(r => r.Payload["content"].StringValue)
            .ToList();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static async Task<List<Dictionary<string, object>>> FetchRowsAsync(
        string connectionString, string tableName, DateTime since, CancellationToken ct)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);

        // tableName may be "Schema.Table" — split and quote each part
        var parts   = tableName.Split('.', 2);
        string qualifiedName = parts.Length == 2
            ? $"[{parts[0]}].[{parts[1]}]"
            : $"[{tableName}]";
        string sql = $"SELECT TOP 5000 * FROM {qualifiedName}";

        await using var cmd    = new SqlCommand(sql, conn) { CommandTimeout = 60 };
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var rows = new List<Dictionary<string, object>>();
        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.IsDBNull(i)) { row[reader.GetName(i)] = string.Empty; continue; }
                try   { row[reader.GetName(i)] = reader.GetValue(i); }
                catch { row[reader.GetName(i)] = reader.GetFieldType(i).Name; } // skip UDT/spatial columns
            }
            rows.Add(row);
        }
        return rows;
    }

    private static string SerializeRow(string dbName, string tableName, Dictionary<string, object> row)
    {
        var parts = row.Select(kv => $"{kv.Key}: {kv.Value}");
        return $"[{dbName}/{tableName}] {string.Join(", ", parts)}";
    }
}
