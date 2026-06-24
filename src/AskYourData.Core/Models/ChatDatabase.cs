namespace AskYourData.Core.Models;

public class ChatDatabase
{
    public string Name              { get; set; } = string.Empty;
    public string DisplayName       { get; set; } = string.Empty;
    public string ConnectionString  { get; set; } = string.Empty;
    public bool   IsReadOnly        { get; set; } = true;
    public List<string>     Topics      { get; set; } = new();
    public List<ChatTable>   Tables      { get; set; } = new();
    public List<TableGroup> TableGroups { get; set; } = new();
}

public class ChatTable
{
    public string TableName         { get; set; } = string.Empty;
    public string Description       { get; set; } = string.Empty;
    public List<string> KeyColumns  { get; set; } = new();
}
