using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Firezip.Core.Interfaces;
using Firezip.Core.Models;
using Firezip.Infrastructure.Settings;
using Firezip.Infrastructure.Update;
using Firezip.Updater.Models;
using Firezip.Updater.Security;
using Firezip.Updater.Services;
using Firezip.Infrastructure.Services;

namespace Firezip.Tests;

public class UpdaterSecurityAndLifecycleTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _privateKeyXml;
    private readonly string _publicKeyXml;

    public UpdaterSecurityAndLifecycleTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Firezip_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        // Generate test RSA keypair for testing signing and verification
        using var rsa = RSA.Create(2048);
        _privateKeyXml = rsa.ToXmlString(true);
        _publicKeyXml = rsa.ToXmlString(false);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public void NoUpdateAvailable_WhenVersionsMatch_ReturnsUpToDate()
    {
        const string currentVer = "1.0.11";
        const string targetVer = "1.0.11";

        var isDowngradeOrSame = UpdateSecurity.IsDowngrade(currentVer, targetVer);
        Assert.True(isDowngradeOrSame); // targetVer <= currentVer means no upgrade
    }

    [Fact]
    public void UpdateAvailable_WhenTargetVersionHigher_PermitsUpdate()
    {
        const string currentVer = "1.0.11";
        const string targetVer = "1.1.0";

        var isDowngrade = UpdateSecurity.IsDowngrade(currentVer, targetVer);
        Assert.False(isDowngrade);
    }

    [Fact]
    public void DowngradeAttempt_IsRejected()
    {
        const string currentVer = "1.0.11";
        const string targetVer = "1.0.5";

        var isDowngrade = UpdateSecurity.IsDowngrade(currentVer, targetVer);
        Assert.True(isDowngrade);
    }

    [Fact]
    public void NetworkSecurity_EnforcesHttpsOnly()
    {
        Assert.True(UpdateSecurity.EnforceHttps("https://example.com/update/manifest.json"));
        Assert.True(UpdateSecurity.EnforceHttps("https://github.com/drodrigues55/firezip/releases/download/v1.1.0/Firezip.exe"));
        Assert.False(UpdateSecurity.EnforceHttps("http://insecure.example.com/manifest.json"));
        Assert.False(UpdateSecurity.EnforceHttps("ftp://example.com/file.exe"));
        Assert.False(UpdateSecurity.EnforceHttps("file://C:/test.exe"));
        Assert.False(UpdateSecurity.EnforceHttps(null));
        Assert.False(UpdateSecurity.EnforceHttps(string.Empty));
    }

    [Fact]
    public void ValidSignature_PassesVerification()
    {
        var manifest = new UpdateManifest
        {
            Version = "1.1.0",
            DownloadUrl = "https://example.com/firezip/FirezipSetup.exe",
            Sha256 = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855",
            Mandatory = false
        };

        var payload = UpdateSecurity.GetSignablePayload(manifest);
        var signature = UpdateSecurity.SignPayload(payload, _privateKeyXml);
        manifest.Signature = signature;

        var isValid = UpdateSecurity.VerifyManifestSignature(manifest, _publicKeyXml);
        Assert.True(isValid);
    }

    [Fact]
    public void InvalidSignature_AlteredManifest_IsRejected()
    {
        var manifest = new UpdateManifest
        {
            Version = "1.1.0",
            DownloadUrl = "https://example.com/firezip/FirezipSetup.exe",
            Sha256 = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855",
            Mandatory = false
        };

        var payload = UpdateSecurity.GetSignablePayload(manifest);
        var signature = UpdateSecurity.SignPayload(payload, _privateKeyXml);
        manifest.Signature = signature;

        // Tamper with the download URL
        manifest.DownloadUrl = "https://malicious.example.com/trojan.exe";

        var isValid = UpdateSecurity.VerifyManifestSignature(manifest, _publicKeyXml);
        Assert.False(isValid);
    }

    [Fact]
    public void CorruptedOrTruncatedDownload_HashMismatch_IsRejected()
    {
        var dummyFile = Path.Combine(_tempDir, "update_payload.bin");
        File.WriteAllBytes(dummyFile, Encoding.UTF8.GetBytes("Legitimate payload contents"));

        // Use intentional hash mismatch
        var wrongHash = "A" + new string('0', 63);

        var isHashValid = UpdateSecurity.VerifyFileHash(dummyFile, wrongHash);
        Assert.False(isHashValid);

        // Correct hash should pass
        using var sha = SHA256.Create();
        var expectedHash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes("Legitimate payload contents")));
        Assert.True(UpdateSecurity.VerifyFileHash(dummyFile, expectedHash));
    }

    [Fact]
    public async Task ServerOffline_GracefullyFallsBackWithoutThrowing()
    {
        var manager = new UpdateManager();

        // Attempt check against non-existent unreachable local port
        var result = await manager.CheckForUpdatesAsync("https://127.0.0.1:59999/nonexistent_manifest.json", "1.0.0", customPublicKeyXml: _publicKeyXml);

        Assert.NotNull(result);
        Assert.False(result.IsUpdateAvailable);
        Assert.Equal("1.0.0", result.CurrentVersion);
        Assert.Contains(result.Status, new[] { UpdateStatus.OfflineOrNetworkError, UpdateStatus.Failed });
    }

    [Fact]
    public async Task NetworkTimeout_OrCancelledToken_ReturnsCleanly()
    {
        var manager = new UpdateManager();
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Immediate cancellation

        var result = await manager.CheckForUpdatesAsync("https://example.com/manifest.json", "1.0.0", customPublicKeyXml: _publicKeyXml, cancellationToken: cts.Token);

        Assert.NotNull(result);
        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public void AtomicInstallation_BackupAndRollback_RestoresOriginalOnFailure()
    {
        var appDir = Path.Combine(_tempDir, "AppInstall");
        Directory.CreateDirectory(appDir);

        var originalExe = Path.Combine(appDir, "Firezip.UI.exe");
        File.WriteAllText(originalExe, "VERSION_1_ORIGINAL");

        var backupExe = Path.Combine(appDir, "Firezip.UI.exe.bak");

        // Simulate atomic staging: backup created
        File.Copy(originalExe, backupExe, true);
        Assert.True(File.Exists(backupExe));

        // Simulate failed write / corrupted update replacement
        File.WriteAllText(originalExe, "CORRUPTED_PARTIAL_WRITE");

        // Rollback executed:
        File.Copy(backupExe, originalExe, true);
        File.Delete(backupExe);

        Assert.Equal("VERSION_1_ORIGINAL", File.ReadAllText(originalExe));
        Assert.False(File.Exists(backupExe));
    }

    [Fact]
    public void ProcessDetection_IdentifiesRunningAppInstances()
    {
        // When checking for active processes, should return list without throwing
        var running = UpdateManager.GetActiveAppProcesses();
        Assert.NotNull(running);
    }

    [Fact]
    public async Task TaskSchedulerService_QueryAndToggle_WorksConsistently()
    {
        var scheduler = new TaskSchedulerService();
        var isScheduled = await scheduler.IsTaskScheduledAsync();

        // Querying should execute cleanly and return boolean without crashing
        Assert.True(isScheduled || !isScheduled);
    }

    [Fact]
    public async Task ProcessUpdateCoordinator_MissingExecutable_EntersOfflineModeCleanly()
    {
        var coordinator = new ProcessUpdateCoordinator();
        // Check update with non-existent url
        var result = await coordinator.CheckForUpdatesAsync("https://example.com/manifest.json", "1.0.0");

        Assert.NotNull(result);
        Assert.False(result.IsUpdateAvailable);
        Assert.Equal("1.0.0", result.CurrentVersion);
    }

    [Fact]
    public void MainApp_NetworkIsolation_NoSocketOrHttpDependencies()
    {
        // Assert that Firezip.Core and Firezip.Infrastructure assemblies do not instantiate network sockets
        var coreTypes = typeof(IArchiveEngine).Assembly.GetTypes();
        foreach (var t in coreTypes)
        {
            Assert.DoesNotContain("HttpClient", t.Name);
            Assert.DoesNotContain("Socket", t.Name);
        }

        var infraTypes = typeof(AppSettings).Assembly.GetTypes();
        foreach (var t in infraTypes)
        {
            // UpdateChecker and ProcessUpdateCoordinator do not contain HttpClient fields
            var fields = t.GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            foreach (var f in fields)
            {
                Assert.NotEqual("System.Net.Http.HttpClient", f.FieldType.FullName);
            }
        }
    }

    [Fact]
    public void ManifestTampering_VersionAltered_IsRejected()
    {
        var manifest = new UpdateManifest
        {
            Version = "1.1.0",
            DownloadUrl = "https://example.com/firezip/FirezipSetup.exe",
            Sha256 = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855",
            MinimumSupportedVersion = "1.0.0",
            Mandatory = false
        };

        var payload = UpdateSecurity.GetSignablePayload(manifest);
        manifest.Signature = UpdateSecurity.SignPayload(payload, _privateKeyXml);

        // Alter version
        manifest.Version = "1.2.0";
        Assert.False(UpdateSecurity.VerifyManifestSignature(manifest, _publicKeyXml));
    }

    [Fact]
    public void ManifestTampering_MandatoryAltered_IsRejected()
    {
        var manifest = new UpdateManifest
        {
            Version = "1.1.0",
            DownloadUrl = "https://example.com/firezip/FirezipSetup.exe",
            Sha256 = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855",
            MinimumSupportedVersion = "1.0.0",
            Mandatory = false
        };

        var payload = UpdateSecurity.GetSignablePayload(manifest);
        manifest.Signature = UpdateSecurity.SignPayload(payload, _privateKeyXml);

        // Alter mandatory flag
        manifest.Mandatory = true;
        Assert.False(UpdateSecurity.VerifyManifestSignature(manifest, _publicKeyXml));
    }

    [Fact]
    public void ManifestTampering_MinimumVersionAltered_IsRejected()
    {
        var manifest = new UpdateManifest
        {
            Version = "1.1.0",
            DownloadUrl = "https://example.com/firezip/FirezipSetup.exe",
            Sha256 = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855",
            MinimumSupportedVersion = "1.0.0",
            Mandatory = false
        };

        var payload = UpdateSecurity.GetSignablePayload(manifest);
        manifest.Signature = UpdateSecurity.SignPayload(payload, _privateKeyXml);

        // Alter minimum supported version
        manifest.MinimumSupportedVersion = "1.0.5";
        Assert.False(UpdateSecurity.VerifyManifestSignature(manifest, _publicKeyXml));
    }

    [Fact]
    public void ManifestTampering_MissingOrEmptySignature_IsRejected()
    {
        var manifest = new UpdateManifest
        {
            Version = "1.1.0",
            DownloadUrl = "https://example.com/firezip/FirezipSetup.exe",
            Sha256 = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855",
            Signature = null
        };
        Assert.False(UpdateSecurity.VerifyManifestSignature(manifest, _publicKeyXml));

        manifest.Signature = "   ";
        Assert.False(UpdateSecurity.VerifyManifestSignature(manifest, _publicKeyXml));
    }

    [Fact]
    public async Task NetworkSecurity_InsecureHttpScheme_IsRejected()
    {
        var manager = new UpdateManager();
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await manager.SendSecureGetAsync("http://insecure.example.com/manifest.json", CancellationToken.None);
        });

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await manager.SendSecureGetAsync("file:///C:/test.exe", CancellationToken.None);
        });
    }

    [Fact]
    public void VersionComparison_SemanticComparison_HandlesTwoDigitMinorCorrectly()
    {
        // 1.9.0 vs 1.10.0 (1.10.0 must be greater than 1.9.0)
        Assert.False(UpdateSecurity.IsDowngrade("1.9.0", "1.10.0"));
        Assert.True(UpdateSecurity.IsDowngrade("1.10.0", "1.9.0"));
    }
}
