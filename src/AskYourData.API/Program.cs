using AskYourData.API.Middleware;
using AskYourData.Core.Interfaces;
using AskYourData.Core.Options;
using AskYourData.Infrastructure.AI;
using AskYourData.Infrastructure.Chatbot;
using AskYourData.Infrastructure.Database;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ────────────────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, lc) =>
    lc.ReadFrom.Configuration(ctx.Configuration));

// ── Options binding ────────────────────────────────────────────────────────
builder.Services.Configure<OllamaOptions>(
    builder.Configuration.GetSection(OllamaOptions.Section));
builder.Services.Configure<OpenAIFallbackOptions>(
    builder.Configuration.GetSection(OpenAIFallbackOptions.Section));
builder.Services.Configure<QdrantOptions>(
    builder.Configuration.GetSection(QdrantOptions.Section));
builder.Services.Configure<ChatbotOptions>(
    builder.Configuration.GetSection(ChatbotOptions.Section));

// ChatDatabases list binding
var erpDatabases = builder.Configuration
    .GetSection("ChatDatabases")
    .Get<List<AskYourData.Core.Models.ChatDatabase>>() ?? new();
builder.Services.AddSingleton(erpDatabases);

// ── Caching ────────────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();

// ── Infrastructure services ────────────────────────────────────────────────
builder.Services.AddSingleton<SemanticKernelFactory>();
builder.Services.AddSingleton<DatabaseRegistry>();
builder.Services.AddScoped<IDatabaseRouter, DatabaseRouter>();
builder.Services.AddScoped<SchemaInspector>();
builder.Services.AddScoped<IntentExtractor>();
builder.Services.AddScoped<ISqlGeneratorService, SqlGeneratorService>();
builder.Services.AddScoped<ISafeQueryExecutor, SafeQueryExecutor>();
builder.Services.AddScoped<IVectorIngestionService, VectorIngestionService>();
builder.Services.AddScoped<IChatbotService, ChatbotService>();

// Startup service: auto-discovers DB tables on boot so first request never fails
builder.Services.AddHostedService<TableDiscoveryStartupService>();

// ── HTTP client for Ollama health check ───────────────────────────────────
builder.Services.AddHttpClient();

// ── Controllers + OpenAPI ─────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// ── CORS (allow frontend on port 4500) ────────────────────────────────────
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod()));

var app = builder.Build();

// ── Middleware pipeline ────────────────────────────────────────────────────
app.UseMiddleware<ExceptionMiddleware>();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Expose Program class for WebApplicationFactory in integration tests
public partial class Program { }
