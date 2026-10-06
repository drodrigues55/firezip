using Firezip.Infrastructure.Update;

namespace Firezip.Tests;

public class UpdateCheckerTests
{
    [Fact]
    public async Task UpdateChecker_WhenCancelled_ReturnsGracefullyWithoutThrowing()
    {
        var checker = new UpdateChecker();
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        var result = await checker.CheckForUpdatesAsync("1.0.0", cts.Token);

        Assert.NotNull(result);
        Assert.False(result.IsUpdateAvailable);
        Assert.Equal("1.0.0", result.CurrentVersion);
    }

    [Fact]
    public async Task UpdateChecker_OfflineFallback_NeverThrows()
    {
        var checker = new UpdateChecker();

        // Even with network issues or rapid timeout, method must complete cleanly
        var result = await checker.CheckForUpdatesAsync("1.0.0");

        Assert.NotNull(result);
        Assert.Equal("1.0.0", result.CurrentVersion);
    }
}
