namespace AskYourData.Core.Models;

/// <summary>
/// Groups related tables for a specific query intent (e.g., "fabric by style").
/// When the intent is detected the chatbot queries ALL tables in the group
/// with a JOIN-capable SQL instead of picking just one table.
/// </summary>
public class TableGroup
{
    /// <summary>Unique name for this group (used in debug info).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Human-readable description passed to the SQL prompt as context.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// One or more keywords (English or Bengali) that must appear in the
    /// question to activate this group.  At least one match is required.
    /// </summary>
    public List<string> TriggerKeywords { get; set; } = new();

    /// <summary>
    /// Optional .NET regex (case-insensitive) with a single capture group (Group 1)
    /// that extracts the entity value from the question.
    /// Example:  (?:style|স্টাইল)\s*(?:no|#|নং)?\s*[:.]?\s*([A-Za-z0-9][A-Za-z0-9-]*)
    /// </summary>
    public string EntityRegex { get; set; } = string.Empty;

    /// <summary>
    /// Label for the entity type — used in the SQL generation prompt
    /// (e.g., "style number", "buyer", "order").
    /// </summary>
    public string EntityLabel { get; set; } = "entity";

    /// <summary>Table names that must be queried together for this intent.</summary>
    public List<string> Tables { get; set; } = new();
}
