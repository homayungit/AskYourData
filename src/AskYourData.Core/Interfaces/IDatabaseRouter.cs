using AskYourData.Core.Models;

namespace AskYourData.Core.Interfaces;

public interface IDatabaseRouter
{
    /// <summary>
    /// Scores all configured databases against the question keywords (Bangla + English)
    /// and returns the best-matching ChatDatabase.
    /// </summary>
    Task<(ChatDatabase Database, string Reason)> RouteAsync(string question, CancellationToken cancellationToken = default);
}
