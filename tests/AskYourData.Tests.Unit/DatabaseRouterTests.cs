using AskYourData.Core.Models;
using AskYourData.Infrastructure.Database;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AskYourData.Tests.Unit;

public class DatabaseRouterTests
{
    private static List<ChatDatabase> BuildDatabases() =>
    [
        new()
        {
            Name = "ProductionDB",
            Topics = ["production", "efficiency", "line", "প্রোডাকশন", "লাইন"],
            Tables = [ new() { TableName = "LineEfficiency", Description = "test" } ]
        },
        new()
        {
            Name = "HrDB",
            Topics = ["hr", "attendance", "salary", "উপস্থিতি", "বেতন"],
            Tables = [ new() { TableName = "Attendance", Description = "test" } ]
        },
        new()
        {
            Name = "InventoryDB",
            Topics = ["inventory", "fabric", "stock", "ফ্যাব্রিক", "স্টক"],
            Tables = [ new() { TableName = "FabricStock", Description = "test" } ]
        }
    ];

    [Fact]
    public async Task RouteAsync_MatchesProductionKeyword_ReturnsProductionDB()
    {
        var router = new DatabaseRouter(BuildDatabases(), NullLogger<DatabaseRouter>.Instance);

        var (db, _) = await router.RouteAsync("আজকের production efficiency কতো?");

        Assert.Equal("ProductionDB", db.Name);
    }

    [Fact]
    public async Task RouteAsync_MatchesHrKeyword_ReturnsHrDB()
    {
        var router = new DatabaseRouter(BuildDatabases(), NullLogger<DatabaseRouter>.Instance);

        var (db, _) = await router.RouteAsync("আজকের attendance কতো?");

        Assert.Equal("HrDB", db.Name);
    }

    [Fact]
    public async Task RouteAsync_MatchesBanglaKeyword_ReturnsCorrectDB()
    {
        var router = new DatabaseRouter(BuildDatabases(), NullLogger<DatabaseRouter>.Instance);

        var (db, _) = await router.RouteAsync("ফ্যাব্রিক স্টক এ কি আছে?");

        Assert.Equal("InventoryDB", db.Name);
    }

    [Fact]
    public async Task RouteAsync_NoMatch_DefaultsToFirstDB()
    {
        var router = new DatabaseRouter(BuildDatabases(), NullLogger<DatabaseRouter>.Instance);

        var (db, reason) = await router.RouteAsync("কোনো keyword নেই এখানে xyz");

        Assert.Equal("ProductionDB", db.Name);
        Assert.Contains("defaulting", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RouteAsync_EmptyDatabases_ThrowsInvalidOperation()
    {
        var router = new DatabaseRouter(new List<ChatDatabase>(), NullLogger<DatabaseRouter>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            router.RouteAsync("anything"));
    }

    [Fact]
    public async Task RouteAsync_ReturnsReason_ContainingDbName()
    {
        var router = new DatabaseRouter(BuildDatabases(), NullLogger<DatabaseRouter>.Instance);

        var (db, reason) = await router.RouteAsync("salary report দাও");

        Assert.Equal("HrDB", db.Name);
        Assert.Contains("HrDB", reason);
    }
}
