namespace AskYourData.Core.Models;

public class QueryResult
{
    public bool Success                              { get; set; }
    public List<Dictionary<string, object>> Rows    { get; set; } = new();
    public int RowCount                             { get; set; }
    public long ExecutionMs                         { get; set; }
    public string? ErrorMessage                     { get; set; }
}
