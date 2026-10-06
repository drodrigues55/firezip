using System.Diagnostics;
using Firezip.Core.Models;
using Firezip.Core.Security;
using Firezip.Formats;
using Xunit;

namespace Firezip.Tests;

public class SecurityTests : IDisposable
{
    private readonly string _safeDestDir;
    private readonly ArchiveEngine _engine;

    public SecurityTests()
    {
        _safeDestDir = Path.Combine(Path.GetTempPath(), "Firezip_Security_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_safeDestDir);
        _engine = new ArchiveEngine();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_safeDestDir))
                Directory.Delete(_safeDestDir, recursive: true);
        }
        catch { }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Path Traversal / Zip Slip
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("../../escape.txt")]
    [InlineData("../escape.txt")]
    [InlineData("sub/../../escape.txt")]
    [InlineData("sub/dir/../../../escape.txt")]
    [InlineData(@"..\..\escape.txt")]
    [InlineData(@"..\Windows\System32\cmd.exe")]
    public void ZipSlip_TraversalSequences_AreBlocked(string maliciousEntryPath)
    {
        var ex = Assert.Throws<SecurityValidationException>(() =>
            PathSanitizer.GetSafeExtractionPath(_safeDestDir, maliciousEntryPath));

        Assert.Equal("PathTraversalSequence", ex.SecurityReason);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Absolute Drive Paths
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(@"C:\Windows\System32\calc.exe")]
    [InlineData(@"D:\Secret\file.txt")]
    [InlineData("c:/malicious/test.exe")]
    public void ZipSlip_AbsoluteDrivePaths_AreBlocked(string maliciousEntryPath)
    {
        var ex = Assert.Throws<SecurityValidationException>(() =>
            PathSanitizer.GetSafeExtractionPath(_safeDestDir, maliciousEntryPath));

        Assert.Equal("AbsoluteDrivePathNotAllowed", ex.SecurityReason);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UNC Paths
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(@"\\evilserver\share\payload.exe")]
    [InlineData(@"//evilserver/share/payload.exe")]
    public void ZipSlip_UncPaths_AreBlocked(string maliciousEntryPath)
    {
        var ex = Assert.Throws<SecurityValidationException>(() =>
            PathSanitizer.GetSafeExtractionPath(_safeDestDir, maliciousEntryPath));

        Assert.Equal("UNCPathNotAllowed", ex.SecurityReason);
    }

    [Fact]
    public async Task ZipSlip_ExtractionDoesNotWriteOutsideDestination()
    {
        var archivePath = Path.Combine(_safeDestDir, "traversal.zip");
        var extractionDir = Path.Combine(_safeDestDir, "extract");
        var escapedName = $"firezip-escaped-{Guid.NewGuid():N}.txt";
        var escapedPath = Path.Combine(Directory.GetParent(_safeDestDir)!.FullName, escapedName);

        try
        {
            using (var file = File.Create(archivePath))
            using (var archive = new System.IO.Compression.ZipArchive(file, System.IO.Compression.ZipArchiveMode.Create))
            using (var writer = new StreamWriter(archive.CreateEntry($"../../{escapedName}").Open()))
            {
                writer.Write("must not escape");
            }

            var result = await _engine.ExtractAsync(new ExtractionRequest
            {
                ArchiveFilePath = archivePath,
                DestinationDirectory = extractionDir,
                DefaultConflictPolicy = ConflictPolicy.Overwrite
            });

            Assert.False(result.Success);
            Assert.False(File.Exists(escapedPath));
        }
        finally
        {
            if (File.Exists(escapedPath))
                File.Delete(escapedPath);
        }
    }

    [Fact]
    public async Task Extraction_BlocksDirectoryJunctionRedirect()
    {
        var extractionDir = Path.Combine(_safeDestDir, "symlink-extract");
        var outsideDir = Path.Combine(_safeDestDir, "symlink-target");
        var linkPath = Path.Combine(extractionDir, "redirect");
        var destinationLink = Path.Combine(_safeDestDir, "destination-link");
        var archivePath = Path.Combine(_safeDestDir, "symlink-entry.zip");
        Directory.CreateDirectory(extractionDir);
        Directory.CreateDirectory(outsideDir);

        try
        {
            CreateDirectoryJunction(linkPath, outsideDir);

            using (var file = File.Create(archivePath))
            using (var archive = new System.IO.Compression.ZipArchive(file, System.IO.Compression.ZipArchiveMode.Create))
            using (var writer = new StreamWriter(archive.CreateEntry("redirect/payload.txt").Open()))
            {
                writer.Write("must not follow the link");
            }

            var result = await _engine.ExtractAsync(new ExtractionRequest
            {
                ArchiveFilePath = archivePath,
                DestinationDirectory = extractionDir,
                DefaultConflictPolicy = ConflictPolicy.Overwrite
            });

            Assert.False(result.Success);
            Assert.False(File.Exists(Path.Combine(outsideDir, "payload.txt")));

            CreateDirectoryJunction(destinationLink, outsideDir);
            var rootLinkException = Assert.Throws<SecurityValidationException>(() =>
                PathSanitizer.ValidateExtractionTarget(
                    Path.Combine(destinationLink, "payload.txt"),
                    destinationLink));
            Assert.Equal("ReparsePointInExtractionPath", rootLinkException.SecurityReason);
        }
        finally
        {
            if (Directory.Exists(linkPath))
                Directory.Delete(linkPath);
            if (Directory.Exists(destinationLink))
                Directory.Delete(destinationLink);
        }
    }

    [Fact]
    public async Task Extraction_CancellationPreservesExistingFileAndRemovesTemporaryOutput()
    {
        var extractionDir = Path.Combine(_safeDestDir, "cancel-extract");
        var archivePath = Path.Combine(_safeDestDir, "cancel-entry.zip");
        var targetPath = Path.Combine(extractionDir, "payload.bin");
        Directory.CreateDirectory(extractionDir);
        await File.WriteAllTextAsync(targetPath, "existing content");

        const int payloadSize = 64 * 1024 * 1024;
        using (var file = File.Create(archivePath))
        using (var archive = new System.IO.Compression.ZipArchive(file, System.IO.Compression.ZipArchiveMode.Create))
        using (var output = archive.CreateEntry("payload.bin", System.IO.Compression.CompressionLevel.NoCompression).Open())
        {
            var buffer = new byte[80 * 1024];
            var random = new Random(42);
            for (int written = 0; written < payloadSize; written += buffer.Length)
            {
                random.NextBytes(buffer);
                output.Write(buffer, 0, Math.Min(buffer.Length, payloadSize - written));
            }
        }

        using var cancellation = new CancellationTokenSource();
        var progress = new CancelOnFirstProgress(cancellation);
        var result = await _engine.ExtractAsync(new ExtractionRequest
        {
            ArchiveFilePath = archivePath,
            DestinationDirectory = extractionDir,
            DefaultConflictPolicy = ConflictPolicy.Overwrite,
            Progress = progress
        }, cancellation.Token);

        Assert.True(result.IsCancelled);
        Assert.True(progress.WasReported);
        Assert.Equal("existing content", await File.ReadAllTextAsync(targetPath));
        Assert.Empty(Directory.GetFiles(extractionDir, ".firezip-*.tmp", SearchOption.TopDirectoryOnly));
    }

    private sealed class CancelOnFirstProgress(CancellationTokenSource cancellation) : IProgress<OperationProgress>
    {
        public bool WasReported { get; private set; }

        public void Report(OperationProgress value)
        {
            WasReported = true;
            cancellation.Cancel();
        }
    }

    private static void CreateDirectoryJunction(string linkPath, string targetPath)
    {
        var startInfo = new ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("mklink");
        startInfo.ArgumentList.Add("/J");
        startInfo.ArgumentList.Add(linkPath);
        startInfo.ArgumentList.Add(targetPath);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start cmd.exe to create a test junction.");
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Could not create a test junction (exit code {process.ExitCode}).");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Windows Reserved Device Names
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("NUL")]
    [InlineData("nul")]
    [InlineData("NUL.txt")]
    [InlineData("CON")]
    [InlineData("con.log")]
    [InlineData("COM1")]
    [InlineData("com9.exe")]
    [InlineData("LPT1")]
    [InlineData("lpt3.dat")]
    [InlineData("AUX")]
    [InlineData("PRN")]
    [InlineData("subdir/NUL")]
    [InlineData("subdir/COM1.dat")]
    public void ReservedDeviceName_IsBlocked(string maliciousEntryPath)
    {
        var ex = Assert.Throws<SecurityValidationException>(() =>
            PathSanitizer.GetSafeExtractionPath(_safeDestDir, maliciousEntryPath));

        Assert.Equal("ReservedDeviceName", ex.SecurityReason);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Alternate Data Streams
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("file.txt:hidden")]
    [InlineData("file.txt::$DATA")]
    [InlineData("sub/file:stream")]
    public void AlternateDataStream_IsBlocked(string maliciousEntryPath)
    {
        var ex = Assert.Throws<SecurityValidationException>(() =>
            PathSanitizer.GetSafeExtractionPath(_safeDestDir, maliciousEntryPath));

        Assert.Equal("IllegalColonInSegment", ex.SecurityReason);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Null Bytes
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void NullByteInEntryPath_IsBlocked()
    {
        var malicious = "valid/path\0hidden.exe";

        var ex = Assert.Throws<SecurityValidationException>(() =>
            PathSanitizer.GetSafeExtractionPath(_safeDestDir, malicious));

        Assert.Equal("NullByteInPath", ex.SecurityReason);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Safe paths (should NOT throw)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SafeEntry_ResolvesInsideTargetDirectory()
    {
        var safeEntry = "documents/readme.txt";
        var resolved = PathSanitizer.GetSafeExtractionPath(_safeDestDir, safeEntry);

        Assert.StartsWith(PathSanitizer.NormalizeDirectory(_safeDestDir), resolved, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("readme.txt", resolved);
    }

    [Theory]
    [InlineData("file.txt")]
    [InlineData("subdir/file.txt")]
    [InlineData("a/b/c/deep.bin")]
    [InlineData("unicode_file_\u00e9.txt")]
    [InlineData("file with spaces.txt")]
    public void SafeEntry_Variants_DoNotThrow(string safeEntry)
    {
        // Should complete without throwing
        var resolved = PathSanitizer.GetSafeExtractionPath(_safeDestDir, safeEntry);
        Assert.StartsWith(
            PathSanitizer.NormalizeDirectory(_safeDestDir),
            resolved,
            StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Unique file path generation
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GenerateUniqueFilePath_AppendsIncrementalSuffixWhenExists()
    {
        var tempFile = Path.Combine(_safeDestDir, "document.txt");
        File.WriteAllText(tempFile, "original");

        var uniquePath1 = PathSanitizer.GenerateUniqueFilePath(tempFile);
        Assert.EndsWith("document (1).txt", uniquePath1);

        File.WriteAllText(uniquePath1, "copy 1");
        var uniquePath2 = PathSanitizer.GenerateUniqueFilePath(tempFile);
        Assert.EndsWith("document (2).txt", uniquePath2);
    }

    [Fact]
    public void GenerateUniqueFilePath_ReturnsOriginal_WhenNoConflict()
    {
        var path = Path.Combine(_safeDestDir, "nonexistent.txt");
        Assert.Equal(path, PathSanitizer.GenerateUniqueFilePath(path));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Decompression Bomb — static header checks
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DecompressionBomb_SuspiciousRatio_IsFlagged()
    {
        var detector = new DecompressionBombDetector(ratioThreshold: 100.0);

        // 100 MB uncompressed from 50 KB compressed = ratio 2000:1
        bool isBomb = detector.IsEntrySuspicious(
            uncompressedSize: 100 * 1024 * 1024,
            compressedSize: 50 * 1024,
            out var reason);

        Assert.True(isBomb);
        Assert.Contains("Abnormally high compression ratio", reason);
    }

    [Fact]
    public void DecompressionBomb_NormalRatio_Passes()
    {
        var detector = new DecompressionBombDetector(ratioThreshold: 100.0);

        // 20 MB uncompressed from 10 MB compressed = 2:1 — fine
        bool isBomb = detector.IsEntrySuspicious(
            uncompressedSize: 20 * 1024 * 1024,
            compressedSize: 10 * 1024 * 1024,
            out var reason);

        Assert.False(isBomb);
        Assert.Empty(reason);
    }

    [Fact]
    public void DecompressionBomb_SmallFile_HighRatio_NotFlagged()
    {
        var detector = new DecompressionBombDetector(ratioThreshold: 200.0);

        // 1 KB uncompressed from 1 byte — absurd ratio but file is below the threshold size,
        // so we do NOT flag it (avoids false positives on trivially small files).
        bool isBomb = detector.IsEntrySuspicious(
            uncompressedSize: 1024,
            compressedSize: 1,
            out var reason);

        Assert.False(isBomb);
        Assert.Empty(reason);
    }

    [Fact]
    public void DecompressionBomb_LargeEntryWithNormalRatio_IsAllowedByDefault()
    {
        var detector = new DecompressionBombDetector();

        // The product has no artificial archive-size limit; a reasonable ratio is allowed.
        bool isBomb = detector.IsEntrySuspicious(
            uncompressedSize: 200L * 1024 * 1024 * 1024,
            compressedSize: 100L * 1024 * 1024 * 1024,
            out var reason);

        Assert.False(isBomb);
        Assert.Empty(reason);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Decompression Bomb — streaming dynamic check
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DecompressionBomb_StreamingCheck_ThrowsWhenBombExceededThreshold()
    {
        // Use very low thresholds to make the test fast
        var detector = new DecompressionBombDetector(
            ratioThreshold: 2.0,
            maxUncompressedEntryBytes: 60 * 1024 * 1024); // 60 MB limit

        // The streaming output cap must stop an entry that exceeds 60 MB,
        // regardless of whether its compression ratio looks suspicious.
        long compressedChunkSize = 1;
        long uncompressedChunkSize = 1024 * 1024; // 1 MB per "read"

        var ex = Assert.Throws<SecurityValidationException>(() =>
        {
            // Drive past the configured per-entry output cap.
            for (int i = 0; i < 150; i++)
            {
                detector.TrackProgress(compressedChunkSize, uncompressedChunkSize);
            }
        });

        Assert.Equal("DecompressionBomb", ex.SecurityReason);
    }

    [Fact]
    public void DecompressionBomb_StreamingCheck_NormalExtraction_Passes()
    {
        var detector = new DecompressionBombDetector();

        // Simulate a normal entry whose output stays below the configured cap.
        for (int i = 0; i < 1000; i++)
        {
            detector.TrackProgress(compressedDelta: 80 * 1024, uncompressedDelta: 80 * 1024);
        }
        // No exception thrown — passes.
    }

    [Fact]
    public void DecompressionBomb_StreamingCheck_EnforcesOutputCapAtOneToOneRatio()
    {
        var detector = new DecompressionBombDetector(ratioThreshold: 2.0, maxUncompressedEntryBytes: 1024);

        detector.TrackProgress(compressedDelta: 1024, uncompressedDelta: 1024);
        var ex = Assert.Throws<SecurityValidationException>(() =>
            detector.TrackProgress(compressedDelta: 1, uncompressedDelta: 1));

        Assert.Equal("DecompressionBomb", ex.SecurityReason);
    }

    [Fact]
    public void DecompressionBomb_StreamingCheck_RejectsOutputBeyondDeclaredEntrySize()
    {
        var detector = new DecompressionBombDetector();

        detector.TrackProgress(compressedDelta: 100, uncompressedDelta: 1024, expectedEntrySize: 1024);
        var ex = Assert.Throws<SecurityValidationException>(() =>
            detector.TrackProgress(compressedDelta: 1, uncompressedDelta: 1, expectedEntrySize: 1024));

        Assert.Equal("DecompressionBomb", ex.SecurityReason);
        Assert.Contains("declared size", ex.Message);
    }

    [Fact]
    public void DecompressionBomb_ResetStartsNewEntryBudget()
    {
        var detector = new DecompressionBombDetector(maxUncompressedEntryBytes: 1024);

        detector.TrackProgress(compressedDelta: 1024, uncompressedDelta: 1024);
        detector.Reset();
        detector.TrackProgress(compressedDelta: 1024, uncompressedDelta: 1024);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Malformed Archive — engine must not crash
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MalformedArchive_OpenAsync_ReturnsErrorGracefully()
    {
        // Create a file with garbage bytes — not a valid archive
        var fakeArchivePath = Path.Combine(_safeDestDir, "malformed.zip");
        await File.WriteAllBytesAsync(fakeArchivePath, [0xFF, 0xFE, 0x00, 0x01, 0xDE, 0xAD, 0xBE, 0xEF]);

        // The engine must not crash the process with an unhandled exception.
        // Acceptable outcomes: a graceful handled exception with a descriptive message,
        // or a returned ArchiveInfo result.
        try
        {
            var archiveInfo = await _engine.OpenArchiveAsync(fakeArchivePath);
            Assert.NotNull(archiveInfo);
        }
        catch (NotSupportedException)
        {
            // Expected: format not recognised by the engine before opening
        }
        catch (SharpCompress.Common.ArchiveOperationException)
        {
            // Expected: SharpCompress could not determine the archive format
        }
        catch (SharpCompress.Common.InvalidFormatException)
        {
            // Expected: SharpCompress detected a malformed archive structure
        }
        catch (Exception ex) when (
            ex.Message.Contains("corrupt", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("not supported", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("Cannot determine", StringComparison.OrdinalIgnoreCase))
        {
            // Expected: archive library threw a descriptive human-readable error
        }
        // Any other exception type that escapes here will fail the test,
        // enforcing that no raw unhandled exception reaches the caller.
    }

    [Fact]
    public async Task MalformedArchive_ExtractAsync_ReturnsFailedResult()
    {
        // Write truncated ZIP magic with garbage payload
        var fakeArchivePath = Path.Combine(_safeDestDir, "truncated.zip");
        await File.WriteAllBytesAsync(fakeArchivePath, [0x50, 0x4B, 0x03, 0x04, 0x00, 0x00]);

        var destDir = Path.Combine(_safeDestDir, "extract_out");

        try
        {
            var result = await _engine.ExtractAsync(new ExtractionRequest
            {
                ArchiveFilePath = fakeArchivePath,
                DestinationDirectory = destDir,
                DefaultConflictPolicy = ConflictPolicy.Overwrite
            });

            // Must not crash the process — either returns a failed result or the library throws a handled exception
            if (result != null)
            {
                Assert.False(result.Success);
            }
        }
        catch (NotSupportedException)
        {
            // Acceptable: format unrecognised before extraction begins
        }
        catch (Exception ex) when (
            ex.Message.Contains("corrupt", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("invalid", StringComparison.OrdinalIgnoreCase))
        {
            // Acceptable: provider surfaced a descriptive library error
        }
    }
}
