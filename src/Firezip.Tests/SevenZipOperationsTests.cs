using Firezip.Core.Models;
using Firezip.Formats;
using Xunit;

namespace Firezip.Tests;

public class SevenZipOperationsTests : IDisposable
{
    private readonly string _testWorkDir;
    private readonly ArchiveEngine _engine;

    public SevenZipOperationsTests()
    {
        _testWorkDir = Path.Combine(Path.GetTempPath(), "Firezip_7zTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testWorkDir);
        _engine = new ArchiveEngine();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testWorkDir))
            {
                Directory.Delete(_testWorkDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task CreateAndExtract7z_FullRoundtrip_Succeeds()
    {
        // 1. Create source file
        var sourceDir = Path.Combine(_testWorkDir, "source7z");
        Directory.CreateDirectory(sourceDir);
        var file = Path.Combine(sourceDir, "data.txt");
        await File.WriteAllTextAsync(file, "Highly compressible repeated text: " + new string('A', 5000));

        // 2. Compress to 7Z
        var sevenZipOutput = Path.Combine(_testWorkDir, "output.7z");
        var compressResult = await _engine.CreateArchiveAsync(new CompressionRequest
        {
            OutputArchiveFilePath = sevenZipOutput,
            Format = ArchiveFormat.SevenZip,
            SourcePaths = [sourceDir],
            Options = new CompressionOptions { Level = CompressionLevel.Normal }
        });

        Assert.True(compressResult.Success, compressResult.ErrorMessage);
        Assert.True(File.Exists(sevenZipOutput));

        // 3. Open archive
        var archiveInfo = await _engine.OpenArchiveAsync(sevenZipOutput);
        Assert.Equal(ArchiveFormat.SevenZip, archiveInfo.Format);
        Assert.True(archiveInfo.TotalFiles >= 1);

        // 4. Test integrity
        var testResult = await _engine.TestArchiveAsync(new TestArchiveRequest
        {
            ArchiveFilePath = sevenZipOutput
        });
        Assert.True(testResult.Success, testResult.ErrorMessage);

        // 5. Extract
        var destDir = Path.Combine(_testWorkDir, "extracted7z");
        var extractResult = await _engine.ExtractAsync(new ExtractionRequest
        {
            ArchiveFilePath = sevenZipOutput,
            DestinationDirectory = destDir,
            DefaultConflictPolicy = ConflictPolicy.Overwrite
        });

        Assert.True(extractResult.Success, extractResult.ErrorMessage);
        var extractedFiles = Directory.GetFiles(destDir, "*.*", SearchOption.AllDirectories);
        Assert.True(extractedFiles.Length >= 1);

        var extractedContent = await File.ReadAllTextAsync(extractedFiles.First(f => f.EndsWith("data.txt")));
        Assert.StartsWith("Highly compressible repeated text: AAAA", extractedContent);
    }

    [Fact]
    public async Task DeleteEntries_RemovesTargetFile_FromSevenZipArchive()
    {
        // 1. Create a 7z with two files
        var fileA = Path.Combine(_testWorkDir, "docA.txt");
        var fileB = Path.Combine(_testWorkDir, "docB.txt");
        await File.WriteAllTextAsync(fileA, "Content A in 7z");
        await File.WriteAllTextAsync(fileB, "Content B in 7z");

        var sevenZipPath = Path.Combine(_testWorkDir, "delete_test.7z");
        var createRes = await _engine.CreateArchiveAsync(new CompressionRequest
        {
            OutputArchiveFilePath = sevenZipPath,
            Format = ArchiveFormat.SevenZip,
            SourcePaths = [fileA, fileB]
        });
        Assert.True(createRes.Success, createRes.ErrorMessage);

        var initialInfo = await _engine.OpenArchiveAsync(sevenZipPath);
        Assert.Equal(2, initialInfo.TotalFiles);

        // 2. Delete docA.txt
        var deleteResult = await _engine.DeleteEntriesAsync(sevenZipPath, ["docA.txt"]);
        Assert.True(deleteResult.Success, deleteResult.ErrorMessage);

        // 3. Verify docA is gone, docB remains intact
        var updatedInfo = await _engine.OpenArchiveAsync(sevenZipPath);
        Assert.Equal(1, updatedInfo.TotalFiles);
        Assert.DoesNotContain(updatedInfo.Entries, e => e.Name == "docA.txt");
        Assert.Contains(updatedInfo.Entries, e => e.Name == "docB.txt");
    }

    [Fact]
    public async Task AddEntries_AppendsNewFile_ToSevenZipArchive()
    {
        // 1. Create a 7z with one file
        var fileA = Path.Combine(_testWorkDir, "existing.txt");
        await File.WriteAllTextAsync(fileA, "Existing content in 7z");

        var sevenZipPath = Path.Combine(_testWorkDir, "add_test.7z");
        var createRes = await _engine.CreateArchiveAsync(new CompressionRequest
        {
            OutputArchiveFilePath = sevenZipPath,
            Format = ArchiveFormat.SevenZip,
            SourcePaths = [fileA]
        });
        Assert.True(createRes.Success, createRes.ErrorMessage);

        // 2. Add a new file to the 7z
        var newFile = Path.Combine(_testWorkDir, "added.txt");
        await File.WriteAllTextAsync(newFile, "Newly added content in 7z");

        var addResult = await _engine.AddEntriesAsync(sevenZipPath, [newFile]);
        Assert.True(addResult.Success, addResult.ErrorMessage);

        // 3. Verify both files are in the 7z and can be extracted
        var updatedInfo = await _engine.OpenArchiveAsync(sevenZipPath);
        Assert.Equal(2, updatedInfo.TotalFiles);
        Assert.Contains(updatedInfo.Entries, e => e.Name == "existing.txt");
        Assert.Contains(updatedInfo.Entries, e => e.Name == "added.txt");

        var extractDir = Path.Combine(_testWorkDir, "add_7z_extracted");
        var extResult = await _engine.ExtractAsync(new ExtractionRequest
        {
            ArchiveFilePath = sevenZipPath,
            DestinationDirectory = extractDir
        });
        Assert.True(extResult.Success, extResult.ErrorMessage);
        Assert.Equal("Newly added content in 7z", await File.ReadAllTextAsync(Path.Combine(extractDir, "added.txt")));
    }
}
