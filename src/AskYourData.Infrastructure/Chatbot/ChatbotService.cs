using System.Text;
using System.Text.Json;
using AskYourData.Core.Interfaces;
using AskYourData.Core.Models;
using AskYourData.Core.Options;
using AskYourData.Infrastructure.AI;
using AskYourData.Infrastructure.Database;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AskYourData.Infrastructure.Chatbot;

/// <summary>
/// Main pipeline — two paths:
///   A) Multi-table path  — when a TableGroup intent is detected (e.g., style query)
///   B) Single-table path — general questions (existing behavior)
///
/// Steps shared by both paths: Route → Schema → SQL → Execute → Vector → Prompt → LLM
/// </summary>
public class ChatbotService : IChatbotService
{
    private readonly IDatabaseRouter _router;
    private readonly SchemaInspector _schemaInspector;
    private readonly ISqlGeneratorService _sqlGenerator;
    private readonly ISafeQueryExecutor _executor;
    private readonly IVectorIngestionService _vectorService;
    private readonly SemanticKernelFactory _kernelFactory;
    private readonly IntentExtractor _intentExtractor;
    private readonly ChatbotOptions _opts;
    private readonly ILogger<ChatbotService> _logger;

    public ChatbotService(
        IDatabaseRouter router,
        SchemaInspector schemaInspector,
        ISqlGeneratorService sqlGenerator,
        ISafeQueryExecutor executor,
        IVectorIngestionService vectorService,
        SemanticKernelFactory kernelFactory,
        IntentExtractor intentExtractor,
        IOptions<ChatbotOptions> opts,
        ILogger<ChatbotService> logger)
    {
        _router           = router;
        _schemaInspector  = schemaInspector;
        _sqlGenerator     = sqlGenerator;
        _executor         = executor;
        _vectorService    = vectorService;
        _kernelFactory    = kernelFactory;
        _intentExtractor  = intentExtractor;
        _opts             = opts.Value;
        _logger           = logger;
    }

    public async Task<ChatResponse> AskAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing question: {Question}", request.Question);

        // Step 1 — Route to database
        var (targetDb, routingReason) = await _router.RouteAsync(request.Question, cancellationToken);

        // Step 2 — Detect intent (multi-table path?)
        var (intentGroup, entityValue) = _intentExtractor.Extract(
            request.Question, targetDb.TableGroups);

        if (intentGroup is not null && intentGroup.Tables.Count > 0)
            return await HandleMultiTableQueryAsync(
                request, targetDb, intentGroup, entityValue, routingReason, cancellationToken);

        // ── Single-table path (original behavior) ─────────────────────────

        // Step 3 — Pick most relevant table
        ChatTable targetTable;
        try
        {
            targetTable = await PickTableAsync(request.Question, targetDb, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "No tables indexed for database {Db}", targetDb.Name);
            return new ChatResponse { Success = false, ErrorMessage = ex.Message };
        }

        // Step 4 — Get live schema (cached)
        string schema = await _schemaInspector.GetSchemaAsync(
            targetDb.ConnectionString, targetTable.TableName, cancellationToken);

        // Step 5 — Generate SQL
        string sql = await _sqlGenerator.GenerateSqlAsync(
            request.Question, targetTable.TableName, schema, cancellationToken);

        // Step 6 — Execute SQL
        var queryResult = await _executor.ExecuteAsync(sql, targetDb.ConnectionString, cancellationToken);

        // Step 7 — Vector augmentation (best-effort)
        List<string> vectorChunks = new();
        try
        {
            vectorChunks = await _vectorService.SearchAsync(
                request.Question, _opts.TopKVectorResults, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Vector search failed — continuing without vector context");
        }

        // Step 8 — Build final prompt
        string finalPrompt = BuildFinalPrompt(request.Question, schema, queryResult, vectorChunks);

        // Step 9 — LLM generates answer
        string answer = await GenerateAnswerAsync(finalPrompt, cancellationToken);

        return new ChatResponse
        {
            Answer  = answer,
            Success = true,
            Debug   = _opts.EnableDebugInfo ? new DebugInfo
            {
                DatabaseUsed  = targetDb.Name,
                TableUsed     = targetTable.TableName,
                GeneratedSql  = sql,
                RowsReturned  = queryResult.RowCount,
                ExecutionMs   = queryResult.ExecutionMs,
                RoutingReason = routingReason
            } : null
        };
    }

    // ── Multi-table path (intent-based) ────────────────────────────────────
    private async Task<ChatResponse> HandleMultiTableQueryAsync(
        ChatRequest request,
        ChatDatabase db,
        Core.Models.TableGroup group,
        string? entityValue,
        string routingReason,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Multi-table path: group={Group}, entity={Entity}, tables={Tables}",
            group.Name, entityValue ?? "(none)", string.Join(", ", group.Tables));

        // Fetch schemas for all tables in the group (cached by SchemaInspector)
        var tableSchemas = new List<(string TableName, string Schema)>();
        foreach (var tableName in group.Tables)
        {
            try
            {
                var schema = await _schemaInspector.GetSchemaAsync(
                    db.ConnectionString, tableName, ct);
                tableSchemas.Add((tableName, schema));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load schema for table {Table} — skipping", tableName);
            }
        }

        if (tableSchemas.Count == 0)
            return new ChatResponse
            {
                Success      = false,
                ErrorMessage = $"Could not load schemas for intent group '{group.Name}'."
            };

        // Generate JOIN-capable SQL across all tables
        string sql = await _sqlGenerator.GenerateMultiTableSqlAsync(
            request.Question, tableSchemas, group.EntityLabel, entityValue, ct);

        // Execute
        var queryResult = await _executor.ExecuteAsync(sql, db.ConnectionString, ct);

        // Filtered vector search — only pull chunks from the relevant tables
        List<string> vectorChunks = new();
        try
        {
            vectorChunks = await _vectorService.SearchFilteredAsync(
                request.Question, group.Tables, _opts.TopKVectorResults, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Filtered vector search failed");
        }

        // Build combined schema context for the prompt
        string combinedSchema = string.Join("\n\n",
            tableSchemas.Select(ts => ts.Schema));

        string finalPrompt = BuildFinalPrompt(
            request.Question, combinedSchema, queryResult, vectorChunks);

        string answer = await GenerateAnswerAsync(finalPrompt, ct);

        return new ChatResponse
        {
            Answer  = answer,
            Success = true,
            Debug   = _opts.EnableDebugInfo ? new DebugInfo
            {
                DatabaseUsed  = db.Name,
                TableUsed     = $"[{group.Name}] {string.Join(", ", group.Tables)}",
                GeneratedSql  = sql,
                RowsReturned  = queryResult.RowCount,
                ExecutionMs   = queryResult.ExecutionMs,
                RoutingReason = $"{routingReason} | Intent={group.Name} | Entity={entityValue ?? "none"}"
            } : null
        };
    }

    // ── Step 2: pick table via LLM ─────────────────────────────────────────
    private async Task<ChatTable> PickTableAsync(
        string question, ChatDatabase db, CancellationToken ct)
    {
        if (db.Tables.Count == 0)
            throw new InvalidOperationException(
                $"Database '{db.Name}' has no tables indexed yet. Please run /ingest first.");

        if (db.Tables.Count == 1)
            return db.Tables[0];

        var tableList = string.Join("\n", db.Tables.Select((t, i) =>
            $"{i + 1}. {t.TableName} — {t.Description}"));

        string prompt = $"""
            Given these tables:
            {tableList}

            Which single table best answers this question: "{question}"?
            Reply with ONLY the table name, nothing else.
            """;

        var kernel  = await _kernelFactory.GetKernelAsync(ct);
        var result  = await kernel.InvokePromptAsync(prompt, cancellationToken: ct);
        string name = result.ToString().Trim();

        var match = db.Tables.FirstOrDefault(t =>
            string.Equals(t.TableName, name, StringComparison.OrdinalIgnoreCase));

        return match ?? db.Tables[0];  // db.Tables.Count >= 2 guaranteed here
    }

    // ── Step 7: build prompt ───────────────────────────────────────────────
    private string BuildFinalPrompt(
        string question,
        string schema,
        QueryResult queryResult,
        List<string> vectorChunks)
    {
        var sb = new StringBuilder();
        sb.AppendLine(_opts.SystemPrompt);
        sb.AppendLine();

        sb.AppendLine("=== TABLE SCHEMA ===");
        sb.AppendLine(schema);
        sb.AppendLine();

        if (queryResult.Success && queryResult.Rows.Count > 0)
        {
            sb.AppendLine("=== LIVE DATA (SQL Result) ===");
            sb.AppendLine(JsonSerializer.Serialize(queryResult.Rows,
                new JsonSerializerOptions { WriteIndented = false }));
            sb.AppendLine();
        }
        else if (!queryResult.Success)
        {
            sb.AppendLine($"=== SQL ERROR === {queryResult.ErrorMessage}");
            sb.AppendLine();
        }

        if (vectorChunks.Count > 0)
        {
            sb.AppendLine("=== ADDITIONAL CONTEXT (Vector Search) ===");
            foreach (var chunk in vectorChunks)
                sb.AppendLine(chunk);
            sb.AppendLine();
        }

        sb.AppendLine("=== QUESTION ===");
        sb.AppendLine(question);
        sb.AppendLine();

        // Explicit language enforcement (Qwen tends to default to Chinese)
        bool hasBengali = question.Any(c => c >= '\u0980' && c <= '\u09FF');
        if (hasBengali)
            sb.AppendLine("⚠️ LANGUAGE RULE: এই প্রশ্নটি বাংলায় লেখা। আপনাকে অবশ্যই সম্পূর্ণ বাংলায় উত্তর দিতে হবে। কোনো অবস্থায় চীনা ভাষায় (Chinese) উত্তর দেওয়া যাবে না।");
        else
            sb.AppendLine("⚠️ LANGUAGE RULE: Answer ONLY in English. Never respond in Chinese or any other language.");

        return sb.ToString();
    }

    // ── Step 8: LLM answer ─────────────────────────────────────────────────
    private async Task<string> GenerateAnswerAsync(string prompt, CancellationToken ct)
    {
        var kernel      = await _kernelFactory.GetKernelAsync(ct);
        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        var history = new ChatHistory();
        history.AddUserMessage(prompt);

        var response = await chatService.GetChatMessageContentAsync(history, cancellationToken: ct);
        return response.Content ?? string.Empty;
    }
}
