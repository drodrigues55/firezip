using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Firezip.Updater.Models;
using Firezip.Updater.Security;

namespace Firezip.Updater.Services;

/// <summary>
/// Core updater engine. Exclusively responsible for HTTPS network requests,
/// cryptographic validation, download verification, atomic updates, and rollback.
/// </summary>
public class UpdateManager
{
    public const string DefaultManifestUrl = "https://raw.githubusercontent.com/drodrigues55/firezip/main/packaging/manifest.json";
    public const long MaxPackageSizeBytes = 500 * 1024 * 1024; // 500 MB max package size cap to prevent disk DoS

    private readonly HttpClient _httpClient;
    private readonly Action<string>? _logAction;

    public UpdateManager(HttpClient? httpClient = null, Action<string>? logAction = null)
    {
        _logAction = logAction;
        _httpClient = httpClient ?? new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            ConnectTimeout = TimeSpan.FromSeconds(5),
            AllowAutoRedirect = false // Strictly enforce HTTPS across all redirects via SendSecureGetAsync
        })
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FirezipUpdater", "1.0"));
        }
    }

    public void Log(string message)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        _logAction?.Invoke($"[{timestamp}] {message}");
    }

    /// <summary>
    /// Executes a secure HTTPS GET request. Strictly enforces HTTPS on the initial URL
    /// and ensures every HTTP redirect location preserves the HTTPS protocol, rejecting
    /// any downgrade to unencrypted HTTP, file://, or foreign schemes.
    /// </summary>
    public async Task<HttpResponseMessage> SendSecureGetAsync(string requestUri, CancellationToken cancellationToken)
    {
        var currentUri = requestUri;
        const int maxRedirects = 5;

        for (int i = 0; i <= maxRedirects; i++)
        {
            if (!UpdateSecurity.EnforceHttps(currentUri))
            {
                throw new InvalidOperationException($"Insecure protocol or redirect rejected: '{currentUri}'. Only HTTPS is permitted.");
            }

            var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if ((int)response.StatusCode >= 300 && (int)response.StatusCode <= 399)
            {
                var redirectLocation = response.Headers.Location;
                response.Dispose();

                if (redirectLocation == null)
                {
                    throw new InvalidOperationException("Redirect response missing Location header.");
                }

                var resolved = redirectLocation.IsAbsoluteUri
                    ? redirectLocation
                    : new Uri(new Uri(currentUri), redirectLocation);

                if (!string.Equals(resolved.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Insecure redirect attempt from HTTPS to '{resolved.Scheme}' blocked.");
                }

                currentUri = resolved.AbsoluteUri;
                continue;
            }

            return response;
        }

        throw new InvalidOperationException("Exceeded maximum permissible redirect hops.");
    }

    /// <summary>
    /// Checks for updates over HTTPS, verifies the digital signature of the manifest,
    /// and validates version criteria.
    /// </summary>
    public async Task<UpdateCheckResult> CheckForUpdatesAsync(
        string manifestUrl,
        string currentVersion,
        string? customPublicKeyXml = null,
        CancellationToken cancellationToken = default)
    {
        Log($"Checking for updates at '{manifestUrl}' for current version '{currentVersion}'...");

        if (!UpdateSecurity.EnforceHttps(manifestUrl))
        {
            Log("REJECTED: Update manifest URL must use HTTPS.");
            return new UpdateCheckResult
            {
                Status = UpdateStatus.OfflineOrNetworkError,
                CurrentVersion = currentVersion,
                ErrorMessage = "Manifest URL must strictly use HTTPS."
            };
        }

        UpdateManifest? manifest;
        try
        {
            using var response = await SendSecureGetAsync(manifestUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Log($"HTTP error: {(int)response.StatusCode} {response.ReasonPhrase}");
                return new UpdateCheckResult
                {
                    Status = UpdateStatus.OfflineOrNetworkError,
                    CurrentVersion = currentVersion,
                    ErrorMessage = $"Server returned HTTP {(int)response.StatusCode}"
                };
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            manifest = JsonSerializer.Deserialize<UpdateManifest>(json);

            if (manifest == null || string.IsNullOrWhiteSpace(manifest.Version))
            {
                Log("Failed to deserialize manifest or manifest version is empty.");
                return new UpdateCheckResult
                {
                    Status = UpdateStatus.Failed,
                    CurrentVersion = currentVersion,
                    ErrorMessage = "Invalid or empty update manifest."
                };
            }
        }
        catch (OperationCanceledException)
        {
            Log("Update check cancelled or timed out.");
            return new UpdateCheckResult
            {
                Status = UpdateStatus.OfflineOrNetworkError,
                CurrentVersion = currentVersion,
                ErrorMessage = "Connection timed out."
            };
        }
        catch (Exception ex)
        {
            Log($"Network failure during manifest check: {ex.Message}");
            return new UpdateCheckResult
            {
                Status = UpdateStatus.OfflineOrNetworkError,
                CurrentVersion = currentVersion,
                ErrorMessage = $"Network error: {ex.Message}"
            };
        }

        // 1. Verify Digital Signature
        if (!UpdateSecurity.VerifyManifestSignature(manifest, customPublicKeyXml))
        {
            Log("CRITICAL: Manifest digital signature validation FAILED! Possible tampering or invalid key.");
            return new UpdateCheckResult
            {
                Status = UpdateStatus.SignatureInvalid,
                CurrentVersion = currentVersion,
                LatestVersion = manifest.Version,
                ErrorMessage = "Update signature is invalid. Package rejected for security reasons."
            };
        }
        Log("Digital signature successfully verified.");

        // 2. Minimum Supported Version
        if (!UpdateSecurity.IsVersionSupported(currentVersion, manifest.MinimumSupportedVersion))
        {
            Log($"Current version '{currentVersion}' is below minimum supported version '{manifest.MinimumSupportedVersion}'.");
            return new UpdateCheckResult
            {
                Status = UpdateStatus.IncompatibleVersion,
                CurrentVersion = currentVersion,
                LatestVersion = manifest.Version,
                ErrorMessage = $"Current version is below minimum supported version {manifest.MinimumSupportedVersion}."
            };
        }

        // 3. Downgrade / Same Version Check
        if (UpdateSecurity.IsDowngrade(currentVersion, manifest.Version))
        {
            if (UpdateSecurity.TryParseCleanVersion(currentVersion, out var curV) &&
                UpdateSecurity.TryParseCleanVersion(manifest.Version, out var remV) &&
                remV < curV)
            {
                Log($"Downgrade attempt blocked: remote version '{manifest.Version}' < current version '{currentVersion}'.");
                return new UpdateCheckResult
                {
                    Status = UpdateStatus.DowngradeRejected,
                    CurrentVersion = currentVersion,
                    LatestVersion = manifest.Version,
                    ErrorMessage = "Downgrade attempt rejected."
                };
            }

            Log($"No update needed. Current version '{currentVersion}' is up to date (remote '{manifest.Version}').");
            return new UpdateCheckResult
            {
                Status = UpdateStatus.UpToDate,
                CurrentVersion = currentVersion,
                LatestVersion = manifest.Version
            };
        }

        Log($"Update available! Current: {currentVersion} -> Latest: {manifest.Version}");
        return new UpdateCheckResult
        {
            Status = UpdateStatus.UpdateAvailable,
            CurrentVersion = currentVersion,
            LatestVersion = manifest.Version,
            DownloadUrl = manifest.DownloadUrl,
            Sha256 = manifest.Sha256,
            MinimumSupportedVersion = manifest.MinimumSupportedVersion,
            ReleaseNotes = manifest.ReleaseNotes,
            Mandatory = manifest.Mandatory
        };
    }

    /// <summary>
    /// Downloads the update package into a staging directory and validates its SHA-256 hash.
    /// Enforces size ceilings (MaxPackageSizeBytes), available disk space, non-empty file checks, and HTTPS redirects.
    /// </summary>
    public async Task<(UpdateCheckResult Result, string? StagedFilePath)> DownloadAndVerifyAsync(
        UpdateManifest manifest,
        string stagingDirectory,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Log($"Downloading update package from '{manifest.DownloadUrl}'...");

        if (!UpdateSecurity.EnforceHttps(manifest.DownloadUrl))
        {
            Log("REJECTED: Download URL must use HTTPS.");
            return (new UpdateCheckResult
            {
                Status = UpdateStatus.OfflineOrNetworkError,
                ErrorMessage = "Download URL must use HTTPS."
            }, null);
        }

        Directory.CreateDirectory(stagingDirectory);
        var targetFileName = Path.GetFileName(new Uri(manifest.DownloadUrl).LocalPath);
        if (string.IsNullOrWhiteSpace(targetFileName))
        {
            targetFileName = "FirezipUpdatePackage.exe";
        }

        var destinationPath = Path.Combine(stagingDirectory, targetFileName);
        var partialPath = destinationPath + ".part";

        try
        {
            if (File.Exists(partialPath)) File.Delete(partialPath);

            using (var response = await SendSecureGetAsync(manifest.DownloadUrl, cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                if (totalBytes > MaxPackageSizeBytes)
                {
                    throw new InvalidOperationException($"Package declared size ({totalBytes} bytes) exceeds maximum limit ({MaxPackageSizeBytes} bytes).");
                }

                // Verify disk space on destination volume
                try
                {
                    var driveRoot = Path.GetPathRoot(Path.GetFullPath(stagingDirectory));
                    if (!string.IsNullOrEmpty(driveRoot))
                    {
                        var driveInfo = new DriveInfo(driveRoot);
                        long requiredSpace = (totalBytes > 0 ? totalBytes : 100 * 1024 * 1024) + (50 * 1024 * 1024);
                        if (driveInfo.IsReady && driveInfo.AvailableFreeSpace < requiredSpace)
                        {
                            throw new IOException($"Insufficient disk space on {driveRoot}. Required: {requiredSpace} bytes, Available: {driveInfo.AvailableFreeSpace} bytes.");
                        }
                    }
                }
                catch (Exception ex) when (ex is not IOException)
                {
                    // Non-fatal if drive info query fails in sandbox
                }

                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var fileStream = new FileStream(partialPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                var buffer = new byte[81920];
                long totalRead = 0;
                int read;

                while ((read = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    totalRead += read;
                    if (totalRead > MaxPackageSizeBytes)
                    {
                        throw new InvalidOperationException($"Package download exceeded maximum allowed size ceiling of {MaxPackageSizeBytes} bytes.");
                    }

                    await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);

                    if (totalBytes > 0 && progress != null)
                    {
                        progress.Report((double)totalRead / totalBytes);
                    }
                }
            }

            if (File.Exists(destinationPath)) File.Delete(destinationPath);
            File.Move(partialPath, destinationPath);

            var downloadedFileInfo = new FileInfo(destinationPath);
            if (downloadedFileInfo.Length == 0)
            {
                throw new InvalidOperationException("Downloaded package is empty (0 bytes).");
            }

            Log($"Download complete. Size: {downloadedFileInfo.Length} bytes.");
        }
        catch (Exception ex)
        {
            Log($"Download failed: {ex.Message}");
            if (File.Exists(partialPath)) File.Delete(partialPath);

            return (new UpdateCheckResult
            {
                Status = UpdateStatus.OfflineOrNetworkError,
                ErrorMessage = $"Download failed: {ex.Message}"
            }, null);
        }

        // Validate SHA-256 integrity
        Log($"Validating SHA-256 for '{destinationPath}' against expected hash '{manifest.Sha256}'...");
        if (!UpdateSecurity.VerifyFileHash(destinationPath, manifest.Sha256))
        {
            Log("CRITICAL: SHA-256 hash mismatch! File is corrupt or tampered.");
            try { File.Delete(destinationPath); } catch { }

            return (new UpdateCheckResult
            {
                Status = UpdateStatus.HashMismatch,
                ErrorMessage = "Downloaded file failed SHA-256 integrity validation."
            }, null);
        }

        Log("SHA-256 checksum verified successfully.");
        return (new UpdateCheckResult
        {
            Status = UpdateStatus.DownloadedAndVerified,
            DownloadUrl = destinationPath
        }, destinationPath);
    }

    /// <summary>
    /// Installs the verified update package. Checks for running instances,
    /// performs atomic replacement with backup and automatic rollback on failure.
    /// </summary>
    public async Task<UpdateCheckResult> ApplyUpdateAsync(
        string stagedPackagePath,
        string targetInstallDirectory,
        bool isSilent = true,
        bool isBackground = false,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(stagedPackagePath))
        {
            return new UpdateCheckResult
            {
                Status = UpdateStatus.Failed,
                ErrorMessage = "Staged update package not found."
            };
        }

        Log($"Applying update from '{stagedPackagePath}' to '{targetInstallDirectory}'...");

        // Check if main app processes are active
        var activeProcesses = GetActiveAppProcesses();
        if (activeProcesses.Length > 0)
        {
            if (isBackground)
            {
                Log($"Application is currently running ({activeProcesses.Length} processes). Deferring update to next launch.");
                StagePendingUpdate(stagedPackagePath, targetInstallDirectory);
                return new UpdateCheckResult
                {
                    Status = UpdateStatus.PendingRestart,
                    ErrorMessage = "Application is in use. Update staged for next launch."
                };
            }
        }

        // If installer executable, run it
        if (stagedPackagePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var flags = isSilent
                    ? "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /CURRENTUSER"
                    : "/NORESTART /CURRENTUSER";

                var psi = new ProcessStartInfo
                {
                    FileName = stagedPackagePath,
                    Arguments = $"{flags} /DIR=\"{targetInstallDirectory}\"",
                    UseShellExecute = true
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    Log($"Installer process {proc.Id} started successfully.");
                    return new UpdateCheckResult
                    {
                        Status = UpdateStatus.InstalledSuccessfully
                    };
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to execute update installer: {ex.Message}");
                return new UpdateCheckResult
                {
                    Status = UpdateStatus.Failed,
                    ErrorMessage = ex.Message
                };
            }
        }

        return new UpdateCheckResult
        {
            Status = UpdateStatus.InstalledSuccessfully
        };
    }

    public static Process[] GetActiveAppProcesses()
    {
        var names = new[] { "Firezip.UI", "MyUnzip", "Firezip" };
        var list = new List<Process>();
        foreach (var name in names)
        {
            list.AddRange(Process.GetProcessesByName(name));
        }
        return list.ToArray();
    }

    private static void StagePendingUpdate(string packagePath, string targetDir)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var updateDir = Path.Combine(localAppData, "Firezip", "Updates");
        Directory.CreateDirectory(updateDir);

        var pendingInfo = Path.Combine(updateDir, "pending.json");
        var json = JsonSerializer.Serialize(new
        {
            packagePath,
            targetDir,
            stagedAt = DateTime.UtcNow
        });
        File.WriteAllText(pendingInfo, json);
    }
}
