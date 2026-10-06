using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Firezip.Updater.Models;
using Firezip.Updater.Services;

namespace Firezip.Updater;

public static class Program
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public static async Task<int> Main(string[] args)
    {
        bool checkNow = false;
        bool isAuto = false;
        bool isApply = false;
        bool jsonOutput = false;
        bool isSilent = false;
        string? manifestUrl = null;
        string? currentVersion = null;
        string? installDir = null;
        string? customPublicKeyXml = null;

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--check-now", StringComparison.OrdinalIgnoreCase)) checkNow = true;
            else if (string.Equals(arg, "--auto", StringComparison.OrdinalIgnoreCase)) isAuto = true;
            else if (string.Equals(arg, "--apply", StringComparison.OrdinalIgnoreCase)) isApply = true;
            else if (string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase)) jsonOutput = true;
            else if (string.Equals(arg, "--silent", StringComparison.OrdinalIgnoreCase)) isSilent = true;
            else if (string.Equals(arg, "--manifest-url", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                manifestUrl = args[++i];
            }
            else if (string.Equals(arg, "--current-version", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                currentVersion = args[++i];
            }
            else if (string.Equals(arg, "--install-dir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                installDir = args[++i];
            }
#if DEBUG
            else if (string.Equals(arg, "--public-key-file", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                var keyFile = args[++i];
                if (File.Exists(keyFile)) customPublicKeyXml = File.ReadAllText(keyFile);
            }
#endif
        }

        // Determine current version if not specified
        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            currentVersion = DetectInstalledAppVersion(installDir);
        }

        if (string.IsNullOrWhiteSpace(manifestUrl))
        {
            manifestUrl = UpdateManager.DefaultManifestUrl;
        }

        if (string.IsNullOrWhiteSpace(installDir))
        {
            installDir = AppContext.BaseDirectory;
        }

        var updateManager = new UpdateManager(logAction: msg =>
        {
            if (!isSilent && !jsonOutput) Console.WriteLine(msg);
        });

        // 1. Manual check mode (--check-now)
        if (checkNow || (!isAuto && !isApply))
        {
            var result = await updateManager.CheckForUpdatesAsync(manifestUrl, currentVersion, customPublicKeyXml);

            if (jsonOutput)
            {
                var json = JsonSerializer.Serialize(result, JsonOpts);
                Console.Out.WriteLine(json);
                Console.Out.Flush();
            }
            else if (!isSilent)
            {
                Console.WriteLine($"Status: {result.Status}");
                Console.WriteLine($"Current Version: {result.CurrentVersion}");
                Console.WriteLine($"Latest Version: {result.LatestVersion}");
                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    Console.WriteLine($"Error: {result.ErrorMessage}");
                }
            }

            return result.Status == UpdateStatus.UpdateAvailable ? 1 : 0;
        }

        // 2. Automatic background mode (Task Scheduler: --auto)
        if (isAuto)
        {
            var checkResult = await updateManager.CheckForUpdatesAsync(manifestUrl, currentVersion, customPublicKeyXml);
            if (checkResult.Status != UpdateStatus.UpdateAvailable)
            {
                return 0; // Up to date or offline, exit cleanly and silently
            }

            // Staging folder for download
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var stagingDir = Path.Combine(localAppData, "Firezip", "Updates", "staging");

            var manifest = new UpdateManifest
            {
                Version = checkResult.LatestVersion,
                DownloadUrl = checkResult.DownloadUrl,
                Sha256 = checkResult.Sha256,
                MinimumSupportedVersion = checkResult.MinimumSupportedVersion,
                Mandatory = checkResult.Mandatory,
                ReleaseNotes = checkResult.ReleaseNotes
            };

            var (downloadResult, stagedPath) = await updateManager.DownloadAndVerifyAsync(manifest, stagingDir);
            if (downloadResult.Status != UpdateStatus.DownloadedAndVerified || string.IsNullOrEmpty(stagedPath))
            {
                return 0; // Download or verification failed, exit cleanly without error popup
            }

            // In background mode, if app is running, stage update for next restart
            var activeProcesses = UpdateManager.GetActiveAppProcesses();
            if (activeProcesses.Length > 0)
            {
                updateManager.Log("Main application is active. Deferring background update to next launch.");
                return 0;
            }

            // App is closed: apply silently in background
            await updateManager.ApplyUpdateAsync(stagedPath, installDir, isSilent: true, isBackground: true);
            return 0;
        }

        // 3. Apply mode (--apply)
        if (isApply)
        {
            var checkResult = await updateManager.CheckForUpdatesAsync(manifestUrl, currentVersion, customPublicKeyXml);
            if (checkResult.Status != UpdateStatus.UpdateAvailable)
            {
                if (jsonOutput)
                {
                    Console.Out.WriteLine(JsonSerializer.Serialize(checkResult, JsonOpts));
                }
                return 0;
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var stagingDir = Path.Combine(localAppData, "Firezip", "Updates", "staging");

            var manifest = new UpdateManifest
            {
                Version = checkResult.LatestVersion,
                DownloadUrl = checkResult.DownloadUrl,
                Sha256 = checkResult.Sha256,
                MinimumSupportedVersion = checkResult.MinimumSupportedVersion,
                Mandatory = checkResult.Mandatory,
                ReleaseNotes = checkResult.ReleaseNotes
            };

            var (downloadResult, stagedPath) = await updateManager.DownloadAndVerifyAsync(manifest, stagingDir);
            if (downloadResult.Status != UpdateStatus.DownloadedAndVerified || string.IsNullOrEmpty(stagedPath))
            {
                if (jsonOutput) Console.Out.WriteLine(JsonSerializer.Serialize(downloadResult, JsonOpts));
                return 2;
            }

            var applyResult = await updateManager.ApplyUpdateAsync(stagedPath, installDir, isSilent: isSilent, isBackground: false);
            if (jsonOutput) Console.Out.WriteLine(JsonSerializer.Serialize(applyResult, JsonOpts));

            return applyResult.Status == UpdateStatus.InstalledSuccessfully ? 0 : 3;
        }

        return 0;
    }

    private static string DetectInstalledAppVersion(string? searchDirectory)
    {
        var dirsToSearch = new List<string> { AppContext.BaseDirectory };
        if (!string.IsNullOrWhiteSpace(searchDirectory)) dirsToSearch.Insert(0, searchDirectory);

        foreach (var dir in dirsToSearch)
        {
            foreach (var exeName in new[] { "Firezip.UI.exe", "MyUnzip.exe", "Firezip.exe" })
            {
                var candidate = Path.Combine(dir, exeName);
                if (File.Exists(candidate))
                {
                    var ver = FileVersionInfo.GetVersionInfo(candidate);
                    if (!string.IsNullOrWhiteSpace(ver.ProductVersion))
                    {
                        return ver.ProductVersion.Trim();
                    }
                    if (!string.IsNullOrWhiteSpace(ver.FileVersion))
                    {
                        return ver.FileVersion.Trim();
                    }
                }
            }
        }

        // Fallback default version
        return "1.0.0";
    }
}
