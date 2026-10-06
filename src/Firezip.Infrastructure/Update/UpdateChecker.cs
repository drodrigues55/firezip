using Firezip.Core.Models;

namespace Firezip.Infrastructure.Update;

/// <summary>
/// Offline-safe update checker. Strictly maintains zero-network isolation in the main application
/// by delegating all checks to the external FirezipUpdater.exe / MyUnzipUpdater.exe process.
/// </summary>
public class UpdateChecker
{
    private readonly ProcessUpdateCoordinator _coordinator;

    public UpdateChecker()
    {
        _coordinator = new ProcessUpdateCoordinator();
    }

    public async Task<UpdateInfo> CheckForUpdatesAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _coordinator.CheckForUpdatesAsync(null, currentVersion, cancellationToken);
        }
        catch
        {
            // Graceful offline fallback: never throw or block
            return new UpdateInfo
            {
                IsUpdateAvailable = false,
                CurrentVersion = currentVersion,
                LatestVersion = currentVersion,
                Status = "OfflineOrUnavailable"
            };
        }
    }
}
