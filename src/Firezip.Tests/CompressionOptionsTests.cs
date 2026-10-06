using System.Security.Cryptography;
using Firezip.Core.Models;
using Firezip.Formats;

namespace Firezip.Tests;

public sealed class CompressionOptionsTests : IDisposable
{
    private const string Password = "Firezip-test-password-42";
    private readonly string _workDirectory = Path.Combine(Path.GetTempPath(), $"Firezip_CompressionOptions_{Guid.NewGuid():N}");
    private readonly ArchiveEngine _engine = new();

    public CompressionOptionsTests()
    {
        Directory.CreateDirectory(_workDirectory);
    }

    [Fact]
    public async Task ZipPassword_EncryptsEntriesAndExtractsWithPassword()
    {
        var source = Path.Combine(_workDirectory, "source");
        Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(Path.Combine(source, "unicode-ação.txt"), "private content");
        var archive = Path.Combine(_workDirectory, "protected.zip");

        var createResult = await _engine.CreateArchiveAsync(new CompressionRequest
        {
            OutputArchiveFilePath = archive,
            Format = ArchiveFormat.Zip,
            SourcePaths = [source],
            Options = new CompressionOptions { Password = Password, Level = CompressionLevel.Normal }
        });

        Assert.True(createResult.Success, createResult.ErrorMessage);
        var archiveInfo = await _engine.OpenArchiveAsync(archive, Password);
        Assert.True(archiveInfo.IsEncrypted);
        Assert.Contains(archiveInfo.Entries, entry => entry.Name == "unicode-ação.txt" && entry.IsEncrypted);

        var destination = Path.Combine(_workDirectory, "zip-extracted");
        var extractResult = await _engine.ExtractAsync(new ExtractionRequest
        {
            ArchiveFilePath = archive,
            DestinationDirectory = destination,
            Password = Password
        });

        Assert.True(extractResult.Success, extractResult.ErrorMessage);
        Assert.Equal("private content", await File.ReadAllTextAsync(Path.Combine(destination, "source", "unicode-ação.txt")));

        var wrongPasswordResult = await _engine.ExtractAsync(new ExtractionRequest
        {
            ArchiveFilePath = archive,
            DestinationDirectory = Path.Combine(_workDirectory, "wrong-password"),
            Password = "wrong"
        });
        Assert.False(wrongPasswordResult.Success);
    }

    [Fact]
    public async Task SevenZipPasswordHeaderEncryptionAndSplitVolumes_RoundtripThroughEngine()
    {
        var source = Path.Combine(_workDirectory, "volume-source");
        Directory.CreateDirectory(source);
        var content = new byte[2 * 1024 * 1024];
        RandomNumberGenerator.Fill(content);
        await File.WriteAllBytesAsync(Path.Combine(source, "random.bin"), content);
        var archive = Path.Combine(_workDirectory, "volumes.7z");

        var createResult = await _engine.CreateArchiveAsync(new CompressionRequest
        {
            OutputArchiveFilePath = archive,
            Format = ArchiveFormat.SevenZip,
            SourcePaths = [source],
            Options = new CompressionOptions
            {
                Password = Password,
                EncryptHeader = true,
                SplitArchive = true,
                VolumeSizeBytes = 256 * 1024,
                Level = CompressionLevel.Normal
            }
        });

        Assert.True(createResult.Success, createResult.ErrorMessage);
        var volumes = Directory.GetFiles(_workDirectory, "volumes.7z.*").Order(StringComparer.Ordinal).ToArray();
        Assert.True(volumes.Length > 1, $"Expected multiple volume files, found {volumes.Length}.");
        Assert.Equal(ArchiveFormat.SevenZip, _engine.DetectFormat(volumes[0]));

        var archiveInfo = await _engine.OpenArchiveAsync(volumes[0], Password);
        Assert.True(archiveInfo.IsEncrypted);
        Assert.Contains(archiveInfo.Entries, entry => entry.Name == "random.bin");

        var destination = Path.Combine(_workDirectory, "volumes-extracted");
        var extractResult = await _engine.ExtractAsync(new ExtractionRequest
        {
            ArchiveFilePath = volumes[0],
            DestinationDirectory = destination,
            Password = Password
        });

        Assert.True(extractResult.Success, extractResult.ErrorMessage);
        Assert.Equal(content, await File.ReadAllBytesAsync(Path.Combine(destination, "volume-source", "random.bin")));
    }

    [Fact]
    public async Task SplitZip_IsRejectedInsteadOfSilentlyCreatingOneFile()
    {
        var source = Path.Combine(_workDirectory, "zip-source.txt");
        await File.WriteAllTextAsync(source, "content");
        var archive = Path.Combine(_workDirectory, "unsupported-split.zip");

        var result = await _engine.CreateArchiveAsync(new CompressionRequest
        {
            OutputArchiveFilePath = archive,
            Format = ArchiveFormat.Zip,
            SourcePaths = [source],
            Options = new CompressionOptions { SplitArchive = true, VolumeSizeBytes = 1024 }
        });

        Assert.False(result.Success);
        Assert.Contains("only for 7z", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(archive));
    }

    [Fact]
    public async Task ExcludePatterns_AreAppliedAndExistingArchiveSurvivesCancellation()
    {
        var source = Path.Combine(_workDirectory, "filter-source");
        Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(Path.Combine(source, "keep.txt"), "keep");
        await File.WriteAllTextAsync(Path.Combine(source, "remove.tmp"), "remove");
        var archive = Path.Combine(_workDirectory, "filtered.zip");

        var createResult = await _engine.CreateArchiveAsync(new CompressionRequest
        {
            OutputArchiveFilePath = archive,
            Format = ArchiveFormat.Zip,
            SourcePaths = [source],
            Options = new CompressionOptions { ExcludePatterns = ["*.tmp"] }
        });

        Assert.True(createResult.Success, createResult.ErrorMessage);
        var archiveInfo = await _engine.OpenArchiveAsync(archive);
        Assert.Contains(archiveInfo.Entries, entry => entry.Name == "keep.txt");
        Assert.DoesNotContain(archiveInfo.Entries, entry => entry.Name == "remove.tmp");

        var originalArchive = await File.ReadAllBytesAsync(archive);
        var largeSource = Path.Combine(_workDirectory, "large.bin");
        var largeContent = new byte[8 * 1024 * 1024];
        RandomNumberGenerator.Fill(largeContent);
        await File.WriteAllBytesAsync(largeSource, largeContent);

        using var cancellation = new CancellationTokenSource();
        var progress = new CancelAfterFirstReadProgress(cancellation);
        var cancelledResult = await _engine.CreateArchiveAsync(new CompressionRequest
        {
            OutputArchiveFilePath = archive,
            Format = ArchiveFormat.Zip,
            SourcePaths = [largeSource],
            Progress = progress
        }, cancellation.Token);

        Assert.True(cancelledResult.IsCancelled);
        Assert.Equal(originalArchive, await File.ReadAllBytesAsync(archive));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_workDirectory))
                Directory.Delete(_workDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Test cleanup is best-effort if an archive engine leaves an OS handle open.
        }
    }

    private sealed class CancelAfterFirstReadProgress(CancellationTokenSource cancellation) : IProgress<OperationProgress>
    {
        public void Report(OperationProgress value)
        {
            if (value.BytesProcessed > 0)
                cancellation.Cancel();
        }
    }
}
