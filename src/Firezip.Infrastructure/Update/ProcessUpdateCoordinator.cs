using System.Diagnostics;
using System.Text.Json;
using Firezip.Core.Interfaces;
using Firezip.Core.Models;

namespace Firezip.Infrastructure.Update;

/// <summary>
/// Process-isolated update coordinator. Ensures the main application makes ZERO network requests.
/// All HTTPS communications, cryptographic validations, and package downloads are strictly
/// delegated to the separate FirezipUpdater.exe / MyUnzipUpdater.exe process.
/// </summary>
public class ProcessUpdateCoordinator : IUpdateCoordinator
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private readonly ISettingsService? _settingsService;
    private readonly ILoggingService? _loggingService;

    public ProcessUpdateCoordinator(ISettingsService? settingsService = null, ILoggingService? loggingService = null)
    {
        _settingsService = settingsService;
        _loggingService = loggingService;
    }

    public async Task<UpdateInfo> CheckForUpdatesAsync(string? manifestUrl = null, string? currentVersion = null, CancellationToken cancellationToken = default)
    {
        var updaterPath = LocateUpdaterExecutable();
        currentVersion ??= GetCurrentAppVersion();

        if (string.IsNullOrWhiteSpace(updaterPath) || !File.Exists(updaterPath))
        {
            _loggingService?.Warn("Updater executable not found. Operating strictly in offline mode.");
            return new UpdateInfo
            {
                IsUpdateAvailable = false,
                CurrentVersion = currentVersion,
                LatestVersion = currentVersion,
                Status = "OfflineOrUnavailable",
                ErrorMessage = "Updater component not installed."
            };
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = updaterPath,
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            // Use ArgumentList to completely prevent argument injection and quoting issues
            psi.ArgumentList.Add("--check-now");
            psi.ArgumentList.Add("--json");
            if (!string.IsNullOrWhiteSpace(manifestUrl))
            {
                psi.ArgumentList.Add("--manifest-url");
                psi.ArgumentList.Add(manifestUrl);
            }
            psi.ArgumentList.Add("--current-version");
            psi.ArgumentList.Add(currentVersion);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            using var process = new Process { StartInfo = psi };
            process.Start();

            try
            {
                var stdoutTask = process.StandardOutput.ReadToEndAsync(linkedCts.Token);
                await process.WaitForExitAsync(linkedCts.Token);

                var stdout = (await stdoutTask).Trim();
                if (!string.IsNullOrWhiteSpace(stdout) && stdout.StartsWith('{') && stdout.EndsWith('}'))
                {
                    var doc = JsonDocument.Parse(stdout);
                    var root = doc.RootElement;

                    var statusStr = root.TryGetProperty("status", out var s) ? s.GetString() ?? "UpToDate" : "UpToDate";
                    var latestVer = root.TryGetProperty("latestVersion", out var lv) ? lv.GetString() ?? currentVersion : currentVersion;
                    var curVer = root.TryGetProperty("currentVersion", out var cv) ? cv.GetString() ?? currentVersion : currentVersion;
                    var dlUrl = root.TryGetProperty("downloadUrl", out var du) ? du.GetString() ?? string.Empty : string.Empty;
                    var notes = root.TryGetProperty("releaseNotes", out var rn) ? rn.GetString() ?? string.Empty : string.Empty;
                    var errMsg = root.TryGetProperty("errorMessage", out var em) ? em.GetString() : null;

                    bool isAvailable = string.Equals(statusStr, "UpdateAvailable", StringComparison.OrdinalIgnoreCase);

                    if (_settingsService != null)
                    {
                        _settingsService.LastUpdateCheckTime = DateTime.Now;
                        _settingsService.LastUpdateCheckResult = statusStr;
                        _ = _settingsService.SaveAsync();
                    }

                    return new UpdateInfo
                    {
                        IsUpdateAvailable = isAvailable,
                        CurrentVersion = curVer,
                        LatestVersion = latestVer,
                        DownloadUrl = dlUrl,
                        ReleaseNotes = notes,
                        Status = statusStr,
                        ErrorMessage = errMsg
                    };
                }

                return new UpdateInfo
                {
                    IsUpdateAvailable = false,
                    CurrentVersion = currentVersion,
                    LatestVersion = currentVersion,
                    Status = "UpToDate"
                };
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch { }

                return new UpdateInfo
                {
                    IsUpdateAvailable = false,
                    CurrentVersion = currentVersion,
                    LatestVersion = currentVersion,
                    Status = cancellationToken.IsCancellationRequested ? "Cancelled" : "Timeout",
                    ErrorMessage = cancellationToken.IsCancellationRequested ? "Operação cancelada." : "Tempo limite de resposta do updater esgotado."
                };
            }
        }
        catch (Exception ex)
        {
            _loggingService?.Warn($"Error launching updater process: {ex.Message}");
            return new UpdateInfo
            {
                IsUpdateAvailable = false,
                CurrentVersion = currentVersion,
                LatestVersion = currentVersion,
                Status = "OfflineOrUnavailable",
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<bool> LaunchUpdateInstallerAsync(string? manifestUrl = null, CancellationToken cancellationToken = default)
    {
        var updaterPath = LocateUpdaterExecutable();
        if (string.IsNullOrWhiteSpace(updaterPath) || !File.Exists(updaterPath))
        {
            return false;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = updaterPath,
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = true // Launch detached
            };
            psi.ArgumentList.Add("--apply");
            if (!string.IsNullOrWhiteSpace(manifestUrl))
            {
                psi.ArgumentList.Add("--manifest-url");
                psi.ArgumentList.Add(manifestUrl);
            }

            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            _loggingService?.Error($"Failed to launch updater installer: {ex.Message}");
            return false;
        }
    }

    public string? LocateUpdaterExecutable()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new List<string>
        {
            Path.Combine(baseDir, "FirezipUpdater.exe"),
            Path.Combine(baseDir, "MyUnzipUpdater.exe"),
            Path.Combine(baseDir, "FirezipUpdater.dll"),
            // Dev directories
            Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\Firezip.Updater\bin\Debug\net10.0-windows\FirezipUpdater.exe")),
            Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\Firezip.Updater\bin\Release\net10.0-windows\FirezipUpdater.exe"))
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string GetCurrentAppVersion()
    {
        var asm = typeof(ProcessUpdateCoordinator).Assembly;
        var ver = asm.GetName().Version;
        return ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "1.0.0";
    }
}
