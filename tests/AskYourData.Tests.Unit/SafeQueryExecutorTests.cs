using AskYourData.Core.Options;
using AskYourData.Infrastructure.Database;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AskYourData.Tests.Unit;

public class SafeQueryExecutorTests
{
    private static SafeQueryExecutor BuildExecutor(int maxRows = 200, int timeoutSeconds = 30)
    {
        var opts = Options.Create(new ChatbotOptions
        {
            MaxContextRows    = maxRows,
            SqlTimeoutSeconds = timeoutSeconds
        });
        return new SafeQueryExecutor(opts, NullLogger<SafeQueryExecutor>.Instance);
    }

    [Theory]
    [InlineData("INSERT INTO t VALUES (1)")]
    [InlineData("UPDATE t SET a=1")]
    [InlineData("DELETE FROM t")]
    [InlineData("DROP TABLE t")]
    [InlineData("TRUNCATE TABLE t")]
    [InlineData("ALTER TABLE t ADD col INT")]
    [InlineData("EXEC sp_something")]
    [InlineData("EXECUTE xp_cmdshell 'dir'")]
    [InlineData("SELECT * FROM t; -- comment")]
    [InlineData("SELECT * FROM t /* inline */")]
    public async Task ExecuteAsync_BlockedKeywords_ReturnsFailed(string sql)
    {
        var executor = BuildExecutor();
        var result   = await executor.ExecuteAsync(sql, "Server=dummy;", CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_NonSelectStatement_ReturnsFailed()
    {
        var executor = BuildExecutor();
        var result   = await executor.ExecuteAsync("CALL something()", "Server=dummy;", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("SELECT", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ValidSelectButBadConnection_ReturnsFailedWithMessage()
    {
        var executor = BuildExecutor();
        var result   = await executor.ExecuteAsync(
            "SELECT TOP 10 * FROM LineEfficiency",
            "Server=invalid_host_xyz;Database=Test;User Id=u;Password=p;TrustServerCertificate=true;Connect Timeout=2;",
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_CaseInsensitiveBlock_Blocked()
    {
        var executor = BuildExecutor();
        var result   = await executor.ExecuteAsync("insert into t values (1)", "Server=dummy;", CancellationToken.None);

        Assert.False(result.Success);
    }
}
