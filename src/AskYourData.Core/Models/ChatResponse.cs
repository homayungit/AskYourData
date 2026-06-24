namespace AskYourData.Core.Models;

public class ChatResponse
{
    public string Answer       { get; set; } = string.Empty;
    public bool   Success      { get; set; }
    public string? ErrorMessage { get; set; }
    public DebugInfo? Debug    { get; set; }
}

public class DebugInfo
{
    public string DatabaseUsed  { get; set; } = string.Empty;
    public string TableUsed     { get; set; } = string.Empty;
    public string GeneratedSql  { get; set; } = string.Empty;
    public int    RowsReturned  { get; set; }
    public long   ExecutionMs   { get; set; }
    public string RoutingReason { get; set; } = string.Empty;
}
