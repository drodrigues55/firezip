using System.Diagnostics;
using Firezip.Core.Interfaces;

namespace Firezip.Infrastructure.Services;

/// <summary>
/// Manages Windows Task Scheduler jobs for the Firezip background updater using schtasks.exe.
/// Idempotent, non-elevated user scheduler integration.
/// </summary>
public class TaskSchedulerService : ITaskSchedulerService
{
    private readonly ILoggingService? _loggingService;

    public string TaskName => @"Firezip\FirezipUpdateTask";

    public TaskSchedulerService(ILoggingService? loggingService = null)
    {
        _loggingService = loggingService;
    }

    public async Task<bool> IsTaskScheduledAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var (exitCode, stdout, _) = await RunSchtasksAsync($"/Query /TN \"{TaskName}\"", cancellationToken);
            return exitCode == 0 && stdout.Contains(TaskName, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _loggingService?.Warn($"Error querying Windows Task Scheduler for '{TaskName}': {ex.Message}");
            return false;
        }
    }

    public async Task<bool> RegisterOrUpdateTaskAsync(string updaterExecutablePath, string frequency = "Daily", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(updaterExecutablePath) || !File.Exists(updaterExecutablePath))
        {
            _loggingService?.Error($"Cannot schedule updater task: executable not found at '{updaterExecutablePath}'.");
            return false;
        }

        try
        {
            var scheduleArgs = frequency.ToLowerInvariant() switch
            {
                "weekly" => "/SC WEEKLY /D MON /ST 11:00",
                "monthly" => "/SC MONTHLY /D 1 /ST 11:00",
                _ => "/SC DAILY /ST 11:00"
            };

            // Command target runs the updater with --auto argument silently
            var arguments = $"/Create /TN \"{TaskName}\" /TR \"\\\"{updaterExecutablePath}\\\" --auto --silent\" {scheduleArgs} /F";

            var (exitCode, stdout, stderr) = await RunSchtasksAsync(arguments, cancellationToken);
            if (exitCode == 0)
            {
                _loggingService?.Info($"Successfully scheduled update task '{TaskName}' with frequency '{frequency}'.");
                return true;
            }

            _loggingService?.Error($"schtasks /Create failed with exit code {exitCode}: {stderr} {stdout}");
            return false;
        }
        catch (Exception ex)
        {
            _loggingService?.Error($"Failed to register task in Windows Task Scheduler: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UnregisterTaskAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var (exitCode, stdout, stderr) = await RunSchtasksAsync($"/Delete /TN \"{TaskName}\" /F", cancellationToken);
            if (exitCode == 0 || stderr.Contains("not find", StringComparison.OrdinalIgnoreCase) || stdout.Contains("not find", StringComparison.OrdinalIgnoreCase))
            {
                _loggingService?.Info($"Successfully removed update task '{TaskName}'.");
                return true;
            }

            _loggingService?.Warn($"schtasks /Delete returned exit code {exitCode}: {stderr}");
            return false;
        }
        catch (Exception ex)
        {
            _loggingService?.Error($"Failed to delete task '{TaskName}' from Task Scheduler: {ex.Message}");
            return false;
        }
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunSchtasksAsync(string arguments, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return (process.ExitCode, stdout, stderr);
    }
}
