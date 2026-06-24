using System.Text.RegularExpressions;
using AskYourData.Core.Models;
using Microsoft.Extensions.Logging;

namespace AskYourData.Infrastructure.AI;

/// <summary>
/// Lightweight intent extractor.
/// Scans the question for TriggerKeywords defined in TableGroups and
/// extracts any entity value (style number, buyer name, etc.) via regex.
/// </summary>
public class IntentExtractor
{
    private readonly ILogger<IntentExtractor> _logger;

    public IntentExtractor(ILogger<IntentExtractor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Returns the first matching TableGroup and the extracted entity value,
    /// or (null, null) if no intent matches.
    /// </summary>
    public (TableGroup? Group, string? EntityValue) Extract(
        string question,
        IReadOnlyList<TableGroup> groups)
    {
        if (groups.Count == 0) return (null, null);

        var lower = question.ToLowerInvariant();

        foreach (var group in groups)
        {
            bool hasKeyword = group.TriggerKeywords
                .Any(k => lower.Contains(k.ToLowerInvariant()));

            if (!hasKeyword) continue;

            string? entityValue = null;

            if (!string.IsNullOrWhiteSpace(group.EntityRegex))
            {
                var match = Regex.Match(question, group.EntityRegex, RegexOptions.IgnoreCase);
                if (match.Success)
                    entityValue = match.Groups.Count > 1 && match.Groups[1].Success
                        ? match.Groups[1].Value.Trim()
                        : match.Value.Trim();
            }

            _logger.LogInformation(
                "Intent detected: group={Group}, entity={Entity}",
                group.Name, entityValue ?? "(none)");

            return (group, entityValue);
        }

        return (null, null);
    }
}
