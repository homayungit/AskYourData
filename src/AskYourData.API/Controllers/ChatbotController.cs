using AskYourData.Core.Interfaces;
using AskYourData.Core.Models;
using AskYourData.Core.Options;
using AskYourData.Infrastructure.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AskYourData.API.Controllers;

[ApiController]
[Route("api/chatbot")]
public class ChatbotController : ControllerBase
{
    private readonly IChatbotService _chatbotService;
    private readonly IVectorIngestionService _ingestionService;
    private readonly List<ChatDatabase> _databases;
    private readonly ChatbotOptions _chatbotOpts;
    private readonly SchemaInspector _schemaInspector;
    private readonly ILogger<ChatbotController> _logger;

    public ChatbotController(
        IChatbotService chatbotService,
        IVectorIngestionService ingestionService,
        List<ChatDatabase> databases,
        IOptions<ChatbotOptions> chatbotOpts,
        SchemaInspector schemaInspector,
        ILogger<ChatbotController> logger)
    {
        _chatbotService   = chatbotService;
        _ingestionService = ingestionService;
        _databases        = databases;
        _chatbotOpts      = chatbotOpts.Value;
        _schemaInspector  = schemaInspector;
        _logger           = logger;
    }

    // POST /api/chatbot/ask
    [HttpPost("ask")]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AskAsync(
        [FromBody] ChatRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(new ChatResponse { Success = false, ErrorMessage = "Question cannot be empty." });

        var response = await _chatbotService.AskAsync(request, cancellationToken);
        return Ok(response);
    }

    // POST /api/chatbot/ingest  — protected by API key header
    [HttpPost("ingest")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Ingest(
        [FromHeader(Name = "X-Api-Key")] string? apiKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey) ||
            apiKey != _chatbotOpts.IngestApiKey)
        {
            _logger.LogWarning("Ingest rejected — invalid X-Api-Key");
            return Unauthorized(new { error = "Invalid or missing X-Api-Key header." });
        }

        // Fire and forget — use CancellationToken.None so the task survives after HTTP response is sent
        _ = Task.Run(async () =>
        {
            try   { await _ingestionService.IngestAllAsync(CancellationToken.None); }
            catch (Exception ex) { _logger.LogError(ex, "Ingest background task failed"); }
        });

        return Accepted(new { message = "Ingestion started in background." });
    }

    // GET /api/chatbot/status
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Status()
    {
        var dbStatuses = _databases.Select(db => new
        {
            db.Name,
            db.DisplayName,
            Tables = db.Tables.Select(t => t.TableName)
        });

        return Ok(new
        {
            Status       = "Running",
            Timestamp    = DateTime.UtcNow,
            Databases    = dbStatuses
        });
    }

    // GET /api/chatbot/databases
    [HttpGet("databases")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Databases()
    {
        var result = _databases.Select(db => new
        {
            db.Name,
            db.DisplayName,
            Tables = db.Tables.Select(t => new
            {
                t.TableName,
                t.Description,
                t.KeyColumns
            })
        });
        return Ok(result);
    }

    // ── Expert schema endpoints — protected by X-Api-Key, NOT exposed in frontend ──

    // GET /api/chatbot/schema
    // Lists all configured databases with their display names.
    [HttpGet("schema")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult SchemaListDatabases(
        [FromHeader(Name = "X-Api-Key")] string? apiKey)
    {
        if (!IsValidApiKey(apiKey)) return Unauthorized(new { error = "Invalid or missing X-Api-Key header." });

        var result = _databases.Select(db => new
        {
            db.Name,
            db.DisplayName,
            IndexedTableCount = db.Tables.Count
        });
        return Ok(result);
    }

    // GET /api/chatbot/schema/{dbName}
    // Lists all tables in the specified database with approximate row counts.
    [HttpGet("schema/{dbName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SchemaListTables(
        string dbName,
        [FromHeader(Name = "X-Api-Key")] string? apiKey,
        CancellationToken cancellationToken)
    {
        if (!IsValidApiKey(apiKey)) return Unauthorized(new { error = "Invalid or missing X-Api-Key header." });

        var db = _databases.FirstOrDefault(d =>
            d.Name.Equals(dbName, StringComparison.OrdinalIgnoreCase));
        if (db is null)
            return NotFound(new { error = $"Database '{dbName}' not configured." });

        var tables = await _schemaInspector.GetTablesAsync(db.ConnectionString, cancellationToken);
        return Ok(new
        {
            db.Name,
            db.DisplayName,
            TableCount = tables.Count,
            Tables = tables
        });
    }

    // GET /api/chatbot/schema/{dbName}/{tableName}
    // Returns all columns for a table: datatype, nullability, max length, and auditable flag.
    [HttpGet("schema/{dbName}/{tableName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SchemaGetColumns(
        string dbName,
        string tableName,
        [FromHeader(Name = "X-Api-Key")] string? apiKey,
        CancellationToken cancellationToken)
    {
        if (!IsValidApiKey(apiKey)) return Unauthorized(new { error = "Invalid or missing X-Api-Key header." });

        var db = _databases.FirstOrDefault(d =>
            d.Name.Equals(dbName, StringComparison.OrdinalIgnoreCase));
        if (db is null)
            return NotFound(new { error = $"Database '{dbName}' not configured." });

        var columns = await _schemaInspector.GetColumnsDetailedAsync(
            db.ConnectionString, tableName, cancellationToken);

        if (columns.Count == 0)
            return NotFound(new { error = $"Table '{tableName}' not found in '{dbName}'." });

        var auditableColumns = columns.Where(c => c.IsAuditable).Select(c => c.ColumnName).ToList();

        return Ok(new
        {
            Database        = db.Name,
            TableName       = tableName,
            TotalColumns    = columns.Count,
            AuditableColumns = auditableColumns,
            Columns         = columns
        });
    }

    private bool IsValidApiKey(string? key) =>
        !string.IsNullOrWhiteSpace(key) && key == _chatbotOpts.IngestApiKey;
}
