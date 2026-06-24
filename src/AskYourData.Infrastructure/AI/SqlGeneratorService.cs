using System.Text.RegularExpressions;
using AskYourData.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace AskYourData.Infrastructure.AI;

/// <summary>
/// Uses Semantic Kernel InvokePromptAsync to generate a safe SELECT SQL
/// from a natural language question + live schema context.
/// </summary>
public class SqlGeneratorService : ISqlGeneratorService
{
    private readonly SemanticKernelFactory _kernelFactory;
    private readonly ILogger<SqlGeneratorService> _logger;

    private static readonly Regex MarkdownBlockRegex =
        new(@"```[\w]*\s*([\s\S]*?)```", RegexOptions.Compiled);

    // Fix LLM syntax error: "SELECT TOP N DISTINCT col" → "SELECT DISTINCT TOP N col"
    private static readonly Regex TopDistinctRegex =
        new(@"SELECT\s+TOP\s+(\d+)\s+DISTINCT\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public SqlGeneratorService(SemanticKernelFactory kernelFactory, ILogger<SqlGeneratorService> logger)
    {
        _kernelFactory = kernelFactory;
        _logger        = logger;
    }

    public async Task<string> GenerateSqlAsync(
        string question,
        string tableName,
        string schemaContext,
        CancellationToken cancellationToken = default)
    {
        var kernel = await _kernelFactory.GetKernelAsync(cancellationToken);

        string prompt = $"""
            You are a SQL Server expert. Generate a T-SQL SELECT query for the question below.

            STRICT RULES:
            - Use SELECT only. Never use INSERT, UPDATE, DELETE, DROP, TRUNCATE, ALTER, CREATE, EXEC, EXECUTE, xp_, sp_.
            - Always include TOP 200 right after SELECT (e.g. SELECT TOP 200 col FROM ...).
            - For COUNT/SUM/AVG aggregate questions use SELECT TOP 200 COUNT(*) — never use window functions (OVER clause).
            - Use descriptive column aliases that reflect what is counted, e.g. COUNT(*) AS EmployeesJoinedAfter2010, COUNT(*) AS TotalSalesOrders2014, SUM(TotalDue) AS TotalRevenue.
            - For "today" or "আজ": use CAST(GETDATE() AS DATE).
            - For "last week" or "গত সপ্তাহ": use DATEADD(day, -7, GETDATE()).
            - For "last month" or "গত মাস": use DATEADD(day, -30, GETDATE()).
            - Return ONLY the raw SQL query — no explanation, no markdown, no code fences, no comments.

            TABLE SCHEMA:
            {schemaContext}

            QUESTION:
            {question}

            SQL:
            """;

        _logger.LogDebug("Generating SQL for table={Table}, question={Question}", tableName, question);

        var result = await kernel.InvokePromptAsync(prompt, cancellationToken: cancellationToken);
        var rawSql = result.ToString().Trim();

        // Strip markdown code fences if LLM adds them anyway
        var match = MarkdownBlockRegex.Match(rawSql);
        if (match.Success)
            rawSql = match.Groups[1].Value.Trim();

        // Fix LLM syntax error: "SELECT TOP N DISTINCT" → "SELECT DISTINCT TOP N"
        rawSql = TopDistinctRegex.Replace(rawSql, "SELECT DISTINCT TOP $1");

        _logger.LogInformation("Generated SQL: {Sql}", rawSql);
        return rawSql;
    }

    public async Task<string> GenerateMultiTableSqlAsync(
        string question,
        IReadOnlyList<(string TableName, string Schema)> tableSchemas,
        string entityLabel,
        string? entityValue,
        CancellationToken cancellationToken = default)
    {
        var kernel = await _kernelFactory.GetKernelAsync(cancellationToken);

        string schemasBlock = string.Join("\n\n",
            tableSchemas.Select(ts => ts.Schema));

        string entityHint = entityValue is not null
            ? $"The question refers to {entityLabel} = '{entityValue}'."
            : $"The question is about {entityLabel}."; 

        string prompt = $"""
            You are a SQL Server expert. Generate a T-SQL SELECT query that answers the question below.
            {entityHint}

            STRICT RULES:
            - Use SELECT only. Never INSERT, UPDATE, DELETE, DROP, TRUNCATE, ALTER, CREATE, EXEC, EXECUTE, xp_, sp_.
            - Always include TOP 200 right after SELECT (e.g. SELECT TOP 200 col FROM ...).
            - For COUNT/SUM/AVG aggregate questions use SELECT TOP 200 COUNT(*) — never use window functions (OVER clause).
            - Use descriptive column aliases: COUNT(*) AS EmployeesJoinedAfter2010, SUM(TotalDue) AS TotalRevenue, etc.
            - For aggregate questions (count, total, sum, average): use GROUP BY with SUM/COUNT/AVG.
            - For ranking/top-N questions: use ORDER BY ... DESC.
            - For date filtering use YEAR(), MONTH(), or BETWEEN with date literals.
            - For "today": use CAST(GETDATE() AS DATE).
            - JOIN tables using their schema-qualified names (e.g., HumanResources.Employee, Sales.SalesOrderHeader).
            - Return ONLY the raw T-SQL query — no explanation, no markdown, no code fences, no comments.

            TABLE SCHEMAS:
            {schemasBlock}

            QUESTION:
            {question}

            SQL:
            """;

        _logger.LogDebug("Generating multi-table SQL for intent={Entity}, question={Question}",
            entityValue, question);

        var result = await kernel.InvokePromptAsync(prompt, cancellationToken: cancellationToken);
        var rawSql = result.ToString().Trim();

        var match = MarkdownBlockRegex.Match(rawSql);
        if (match.Success)
            rawSql = match.Groups[1].Value.Trim();

        // Fix LLM syntax error: "SELECT TOP N DISTINCT" → "SELECT DISTINCT TOP N"
        rawSql = TopDistinctRegex.Replace(rawSql, "SELECT DISTINCT TOP $1");

        _logger.LogInformation("Generated multi-table SQL: {Sql}", rawSql);
        return rawSql;
    }
}
