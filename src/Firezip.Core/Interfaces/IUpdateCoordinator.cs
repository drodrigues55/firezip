using Firezip.Core.Models;

namespace Firezip.Core.Interfaces;

public interface IUpdateCoordinator
{
    Task<UpdateInfo> CheckForUpdatesAsync(string? manifestUrl = null, string? currentVersion = null, CancellationToken cancellationToken = default);
    Task<bool> LaunchUpdateInstallerAsync(string? manifestUrl = null, CancellationToken cancellationToken = default);
    string? LocateUpdaterExecutable();
}
