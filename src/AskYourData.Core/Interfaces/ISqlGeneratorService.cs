namespace AskYourData.Core.Interfaces;

public interface ISqlGeneratorService
{
    /// <summary>
    /// Generates a safe, read-only SQL SELECT query from a natural language question
    /// using the provided table schema context.
    /// </summary>
    Task<string> GenerateSqlAsync(string question, string tableName, string schemaContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a multi-table JOIN SQL from a natural language question
    /// using schemas for all provided tables.  entityLabel / entityValue provide
    /// extra context (e.g., entityLabel="style number", entityValue="56566").
    /// </summary>
    Task<string> GenerateMultiTableSqlAsync(
        string question,
        IReadOnlyList<(string TableName, string Schema)> tableSchemas,
        string entityLabel,
        string? entityValue,
        CancellationToken cancellationToken = default);
}
