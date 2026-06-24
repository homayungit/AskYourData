namespace AskYourData.Core.Interfaces;

public interface IVectorIngestionService
{
    /// <summary>Ingest all rows from a specific table into the Qdrant vector store.</summary>
    Task IngestTableAsync(string dbName, string tableName, string connectionString, CancellationToken cancellationToken = default);

    /// <summary>Iterate all configured databases and tables and ingest them.</summary>
    Task IngestAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Search the vector store for chunks semantically similar to the question.
    /// Returns up to topK result strings.
    /// </summary>
    Task<List<string>> SearchAsync(string question, int topK = 5, CancellationToken cancellationToken = default);

    /// <summary>
    /// Like SearchAsync but restricts results to the specified table names only
    /// (uses Qdrant payload filter on the "tableName" field).
    /// </summary>
    Task<List<string>> SearchFilteredAsync(string question, IReadOnlyList<string> tableNames, int topK = 5, CancellationToken cancellationToken = default);
}
