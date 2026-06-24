using AskYourData.Core.Interfaces;
using AskYourData.Core.Models;
using Microsoft.Extensions.Logging;

namespace AskYourData.Infrastructure.Database;

/// <summary>
/// Scores each configured ERP database by counting keyword matches
/// (English + Bengali) in the incoming question and returns the best match.
/// </summary>
public class DatabaseRouter : IDatabaseRouter
{
    private readonly List<ChatDatabase> _databases;
    private readonly ILogger<DatabaseRouter> _logger;

    public DatabaseRouter(List<ChatDatabase> databases, ILogger<DatabaseRouter> logger)
    {
        _databases = databases;
        _logger    = logger;
    }

    public Task<(ChatDatabase Database, string Reason)> RouteAsync(
        string question,
        CancellationToken cancellationToken = default)
    {
        if (_databases.Count == 0)
            throw new InvalidOperationException("No ERP databases are configured.");

        var lowerQuestion = question.ToLowerInvariant();

        ChatDatabase? bestDb     = null;
        int          bestScore  = -1;
        string       bestReason = string.Empty;

        foreach (var db in _databases)
        {
            int score = db.Topics.Count(topic =>
                lowerQuestion.Contains(topic.ToLowerInvariant()));

            _logger.LogDebug("DB={Name} Score={Score}", db.Name, score);

            if (score > bestScore)
            {
                bestScore  = score;
                bestDb     = db;
                bestReason = $"Matched {score} keyword(s) in '{db.Name}': " +
                             string.Join(", ", db.Topics
                                 .Where(t => lowerQuestion.Contains(t.ToLowerInvariant())));
            }
        }

        // Fallback: first database
        bestDb ??= _databases[0];
        if (bestScore <= 0)
            bestReason = $"No keyword matched — defaulting to first DB '{bestDb.Name}'";

        _logger.LogInformation("Routed to DB={Name} | Reason={Reason}", bestDb.Name, bestReason);

        return Task.FromResult((bestDb, bestReason));
    }
}
