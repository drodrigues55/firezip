namespace Firezip.Core.Interfaces;

/// <summary>
/// Service responsible for managing the Windows Task Scheduler automatic update job.
/// Executes exclusively the isolated updater component, never the main application.
/// </summary>
public interface ITaskSchedulerService
{
    string TaskName { get; }

    Task<bool> IsTaskScheduledAsync(CancellationToken cancellationToken = default);

    Task<bool> RegisterOrUpdateTaskAsync(string updaterExecutablePath, string frequency = "Daily", CancellationToken cancellationToken = default);

    Task<bool> UnregisterTaskAsync(CancellationToken cancellationToken = default);
}
