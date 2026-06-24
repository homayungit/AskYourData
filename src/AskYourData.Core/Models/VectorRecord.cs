namespace AskYourData.Core.Models;

public class VectorRecord
{
    public Guid   Id          { get; set; } = Guid.NewGuid();
    public string DbName      { get; set; } = string.Empty;
    public string TableName   { get; set; } = string.Empty;
    public string RowKey      { get; set; } = string.Empty;
    public string Content     { get; set; } = string.Empty;
    public DateTime IngestedAt { get; set; } = DateTime.UtcNow;
    public float[] Embedding  { get; set; } = Array.Empty<float>();
}
