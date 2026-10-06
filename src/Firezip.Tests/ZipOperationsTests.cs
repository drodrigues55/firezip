using Firezip.Core.Models;
using Firezip.Formats;
using Xunit;

namespace Firezip.Tests;

public class ZipOperationsTests : IDisposable
{
    private readonly string _testWorkDir;
    private readonly ArchiveEngine _engine;

    public ZipOperationsTests()
    {
        _testWorkDir = Path.Combine(Path.GetTempPath(), "Firezip_ZipTest_" + Guid.NewGuid().ToString("N"));
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
    public async Task CreateAndExtractZip_FullRoundtrip_Succeeds()
    {
        // 1. Create source test directory structure
        var sourceDir = Path.Combine(_testWorkDir, "source");
        Directory.CreateDirectory(sourceDir);
        var subDir = Path.Combine(sourceDir, "subfolder");
        Directory.CreateDirectory(subDir);

        var file1 = Path.Combine(sourceDir, "file1.txt");
        var file2 = Path.Combine(subDir, "file2.txt");
        await File.WriteAllTextAsync(file1, "Hello from file 1 in root");
        await File.WriteAllTextAsync(file2, "Hello from file 2 in subfolder");

        // 2. Compress to ZIP
        var zipOutput = Path.Combine(_testWorkDir, "output.zip");
        var compressResult = await _engine.CreateArchiveAsync(new CompressionRequest
        {
            OutputArchiveFilePath = zipOutput,
            Format = ArchiveFormat.Zip,
            SourcePaths = [sourceDir],
            Options = new CompressionOptions { Level = CompressionLevel.Normal }
        });

        Assert.True(compressResult.Success, compressResult.ErrorMessage);
        Assert.True(File.Exists(zipOutput));

        // 3. Open archive and verify entries
        var archiveInfo = await _engine.OpenArchiveAsync(zipOutput);
        Assert.Equal(ArchiveFormat.Zip, archiveInfo.Format);
        Assert.Equal(2, archiveInfo.TotalFiles);
        Assert.Contains(archiveInfo.Entries, e => e.Name == "file1.txt");
        Assert.Contains(archiveInfo.Entries, e => e.Name == "file2.txt");

        // 4. Test integrity
        var testResult = await _engine.TestArchiveAsync(new TestArchiveRequest
        {
            ArchiveFilePath = zipOutput
        });
        Assert.True(testResult.Success, testResult.ErrorMessage);

        // 5. Extract to destination
        var destDir = Path.Combine(_testWorkDir, "extracted");
        var extractResult = await _engine.ExtractAsync(new ExtractionRequest
        {
            ArchiveFilePath = zipOutput,
            DestinationDirectory = destDir,
            DefaultConflictPolicy = ConflictPolicy.Overwrite
        });

        Assert.True(extractResult.Success, extractResult.ErrorMessage);
        Assert.Equal(2, extractResult.FilesProcessed);

        // Verify extracted files match original content
        var extractedFiles = Directory.GetFiles(destDir, "*.*", SearchOption.AllDirectories);
        Assert.Equal(2, extractedFiles.Length);

        var extractedContent1 = await File.ReadAllTextAsync(extractedFiles.First(f => f.EndsWith("file1.txt")));
        var extractedContent2 = await File.ReadAllTextAsync(extractedFiles.First(f => f.EndsWith("file2.txt")));
        Assert.Equal("Hello from file 1 in root", extractedContent1);
        Assert.Equal("Hello from file 2 in subfolder", extractedContent2);
    }

    [Fact]
    public async Task Extract_ConflictSkip_PreservesExistingFile()
    {
        // 1. Create a test archive with "test.txt" = "NEW CONTENT"
        var sourceDir = Path.Combine(_testWorkDir, "source_conflict");
        Directory.CreateDirectory(sourceDir);
        var sourceFile = Path.Combine(sourceDir, "test.txt");
        await File.WriteAllTextAsync(sourceFile, "NEW CONTENT");

        var zipOutput = Path.Combine(_testWorkDir, "conflict.zip");
        await _engine.CreateArchiveAsync(new CompressionRequest
        {
            OutputArchiveFilePath = zipOutput,
            Format = ArchiveFormat.Zip,
            SourcePaths = [sourceDir]
        });

        // 2. Pre-create the destination file with "ORIGINAL CONTENT"
        var destDir = Path.Combine(_testWorkDir, "dest_conflict");
        Directory.CreateDirectory(destDir);
        var destFile = Path.Combine(destDir, "source_conflict", "test.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
        await File.WriteAllTextAsync(destFile, "ORIGINAL CONTENT");

        // 3. Extract with ConflictPolicy.Skip
        var extractResult = await _engine.ExtractAsync(new ExtractionRequest
        {
            ArchiveFilePath = zipOutput,
            DestinationDirectory = destDir,
            DefaultConflictPolicy = ConflictPolicy.Skip
        });

        Assert.True(extractResult.Success);
        Assert.Equal(1, extractResult.FilesSkipped);

        // Destination file should remain unchanged
        var finalContent = await File.ReadAllTextAsync(destFile);
        Assert.Equal("ORIGINAL CONTENT", finalContent);
    }

    [Fact]
    public async Task DeleteEntries_RemovesTargetFile_FromZipArchive()
    {
        // 1. Create a zip with two files
        var fileA = Path.Combine(_testWorkDir, "docA.txt");
        var fileB = Path.Combine(_testWorkDir, "docB.txt");
        await File.WriteAllTextAsync(fileA, "Content A");
        await File.WriteAllTextAsync(fileB, "Content B");

        var zipPath = Path.Combine(_testWorkDir, "delete_test.zip");
        var createRes = await _engine.CreateArchiveAsync(new CompressionRequest
        {
            OutputArchiveFilePath = zipPath,
            Format = ArchiveFormat.Zip,
            SourcePaths = [fileA, fileB]
        });
        Assert.True(createRes.Success, createRes.ErrorMessage);

        var initialInfo = await _engine.OpenArchiveAsync(zipPath);
        Assert.Equal(2, initialInfo.TotalFiles);

        // 2. Delete docA.txt
        var deleteResult = await _engine.DeleteEntriesAsync(zipPath, ["docA.txt"]);
        Assert.True(deleteResult.Success, deleteResult.ErrorMessage);
        Assert.Equal(1, deleteResult.FilesProcessed);

        // 3. Verify docA is gone, docB remains intact
        var updatedInfo = await _engine.OpenArchiveAsync(zipPath);
        Assert.Equal(1, updatedInfo.TotalFiles);
        Assert.DoesNotContain(updatedInfo.Entries, e => e.Name == "docA.txt");
        Assert.Contains(updatedInfo.Entries, e => e.Name == "docB.txt");
    }

    [Fact]
    public async Task AddEntries_AppendsNewFile_ToZipArchive()
    {
        // 1. Create a zip with one file
        var fileA = Path.Combine(_testWorkDir, "existing.txt");
        await File.WriteAllTextAsync(fileA, "Existing content");

        var zipPath = Path.Combine(_testWorkDir, "add_test.zip");
        var createRes = await _engine.CreateArchiveAsync(new CompressionRequest
        {
            OutputArchiveFilePath = zipPath,
            Format = ArchiveFormat.Zip,
            SourcePaths = [fileA]
        });
        Assert.True(createRes.Success, createRes.ErrorMessage);

        // 2. Add a new file to the zip
        var newFile = Path.Combine(_testWorkDir, "added.txt");
        await File.WriteAllTextAsync(newFile, "Newly added content");

        var addResult = await _engine.AddEntriesAsync(zipPath, [newFile]);
        Assert.True(addResult.Success, addResult.ErrorMessage);

        // 3. Verify both files are in the zip and can be extracted
        var updatedInfo = await _engine.OpenArchiveAsync(zipPath);
        Assert.Equal(2, updatedInfo.TotalFiles);
        Assert.Contains(updatedInfo.Entries, e => e.Name == "existing.txt");
        Assert.Contains(updatedInfo.Entries, e => e.Name == "added.txt");

        var extractDir = Path.Combine(_testWorkDir, "add_extracted");
        var extResult = await _engine.ExtractAsync(new ExtractionRequest
        {
            ArchiveFilePath = zipPath,
            DestinationDirectory = extractDir
        });
        Assert.True(extResult.Success, extResult.ErrorMessage);
        Assert.Equal("Newly added content", await File.ReadAllTextAsync(Path.Combine(extractDir, "added.txt")));
    }
}
