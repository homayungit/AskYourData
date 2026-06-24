using AskYourData.Core.Models;
using Microsoft.Extensions.Logging;

namespace AskYourData.Infrastructure.Database;

/// <summary>
/// Holds and validates the list of configured ERP databases at startup.
/// </summary>
public class DatabaseRegistry
{
    private readonly List<ChatDatabase> _databases;
    private readonly ILogger<DatabaseRegistry> _logger;

    public DatabaseRegistry(List<ChatDatabase> databases, ILogger<DatabaseRegistry> logger)
    {
        _databases = databases;
        _logger    = logger;

        if (_databases.Count == 0)
            _logger.LogWarning("No ERP databases configured in appsettings.json:ChatDatabases");
        else
            _logger.LogInformation("DatabaseRegistry loaded {Count} database(s): {Names}",
                _databases.Count,
                string.Join(", ", _databases.Select(d => d.Name)));
    }

    public IReadOnlyList<ChatDatabase> All => _databases;
}
