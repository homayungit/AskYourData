using AskYourData.Core.Models;

namespace AskYourData.Core.Options;

public class OllamaOptions
{
    public const string Section = "OllamaSettings";

    public string BaseUrl               { get; set; } = "http://localhost:11434";
    public string ChatModel             { get; set; } = "qwen2.5:32b-instruct-q4_K_M";
    public string EmbeddingModel        { get; set; } = "bge-m3";
    public int    NumCtx                { get; set; } = 16384;
    public double Temperature           { get; set; } = 0.1;
    public double TopP                  { get; set; } = 0.9;
    public double RepeatPenalty         { get; set; } = 1.05;
    public string KeepAlive             { get; set; } = "30m";
    public int    RequestTimeoutSeconds { get; set; } = 120;
}

public class OpenAIFallbackOptions
{
    public const string Section = "OpenAIFallback";

    public bool   Enabled        { get; set; } = false;
    public string ApiKey         { get; set; } = string.Empty;
    public string ChatModel      { get; set; } = "gpt-4o-mini";
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
}

public class QdrantOptions
{
    public const string Section = "QdrantSettings";

    public string Host           { get; set; } = "localhost";
    public int    Port           { get; set; } = 6333;
    public string CollectionName { get; set; } = "erp_chunks";
}

public class ChatbotOptions
{
    public const string Section = "ChatbotSettings";

    public int    MaxContextRows      { get; set; } = 200;
    public int    TopKVectorResults   { get; set; } = 5;
    public int    SqlTimeoutSeconds   { get; set; } = 30;
    public bool   EnableDebugInfo     { get; set; } = true;
    public string SystemPrompt        { get; set; } =
        "This is Snowtex chatbot, an ERP assistant for a garment manufacturing company. " +
        "Answer questions based ONLY on the provided data. Always include numbers. " +
        "If data is missing, say so clearly. " +
        "Answer in the same language as the question (Bengali or English).";
    public string IngestApiKey        { get; set; } = string.Empty;
}

public class ChatDatabasesOptions
{
    public List<ChatDatabase> ChatDatabases { get; set; } = new();
}
