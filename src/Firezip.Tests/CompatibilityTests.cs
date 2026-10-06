using Firezip.Core.Models;
using Firezip.Formats;
using Xunit;

namespace Firezip.Tests;

public class CompatibilityTests : IDisposable
{
    private readonly string _testWorkDir;
    private readonly ArchiveEngine _engine;

    public CompatibilityTests()
    {
        _testWorkDir = Path.Combine(Path.GetTempPath(), "Firezip_CompatTest_" + Guid.NewGuid().ToString("N"));
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
    public async Task TarArchive_CreateAndExtract_Roundtrip_PreservesData()
    {
        var sourceDir = Path.Combine(_testWorkDir, "source_tar");
        Directory.CreateDirectory(sourceDir);
        var testFile = Path.Combine(sourceDir, "test_tar.txt");
        await File.WriteAllTextAsync(testFile, "Hello Tar Archive Content 12345");

        var tarOutput = Path.Combine(_testWorkDir, "output.tar");
        var compResult = await _engine.CreateArchiveAsync(new CompressionRequest
        {
            OutputArchiveFilePath = tarOutput,
            Format = ArchiveFormat.Tar,
            SourcePaths = [sourceDir]
        });

        Assert.True(compResult.Success, compResult.ErrorMessage);
        Assert.True(File.Exists(tarOutput));

        var info = await _engine.OpenArchiveAsync(tarOutput);
        Assert.Equal(ArchiveFormat.Tar, info.Format);
        Assert.True(info.TotalFiles >= 1);

        var extractDir = Path.Combine(_testWorkDir, "extracted_tar");
        var extResult = await _engine.ExtractAsync(new ExtractionRequest
        {
            ArchiveFilePath = tarOutput,
            DestinationDirectory = extractDir
        });

        Assert.True(extResult.Success, extResult.ErrorMessage);
        var extractedFiles = Directory.GetFiles(extractDir, "*.*", SearchOption.AllDirectories);
        Assert.True(extractedFiles.Length >= 1);
        var content = await File.ReadAllTextAsync(extractedFiles.First(f => f.EndsWith("test_tar.txt", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal("Hello Tar Archive Content 12345", content);
    }

    [Fact]
    public async Task GZipArchive_CreateAndExtract_Roundtrip_PreservesData()
    {
        var sourceDir = Path.Combine(_testWorkDir, "source_gz");
        Directory.CreateDirectory(sourceDir);
        var testFile = Path.Combine(sourceDir, "test_gz.txt");
        await File.WriteAllTextAsync(testFile, "Hello GZip Archive Content ABCDE");

        var gzOutput = Path.Combine(_testWorkDir, "output.tar.gz");
        var compResult = await _engine.CreateArchiveAsync(new CompressionRequest
        {
            OutputArchiveFilePath = gzOutput,
            Format = ArchiveFormat.GZip,
            SourcePaths = [sourceDir]
        });

        Assert.True(compResult.Success, compResult.ErrorMessage);
        Assert.True(File.Exists(gzOutput));

        var info = await _engine.OpenArchiveAsync(gzOutput);
        Assert.Equal(ArchiveFormat.GZip, info.Format);
        Assert.True(info.TotalFiles >= 1);

        var extractDir = Path.Combine(_testWorkDir, "extracted_gz");
        var extResult = await _engine.ExtractAsync(new ExtractionRequest
        {
            ArchiveFilePath = gzOutput,
            DestinationDirectory = extractDir
        });

        Assert.True(extResult.Success, extResult.ErrorMessage);
        var extractedFiles = Directory.GetFiles(extractDir, "*.*", SearchOption.AllDirectories);
        Assert.True(extractedFiles.Length >= 1);
        var content = await File.ReadAllTextAsync(extractedFiles.First(f => f.EndsWith("test_gz.txt", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal("Hello GZip Archive Content ABCDE", content);
    }

    [Fact]
    public async Task BZip2Archive_CreateAndExtract_Roundtrip_PreservesData()
    {
        var sourceDir = Path.Combine(_testWorkDir, "source_bz2");
        Directory.CreateDirectory(sourceDir);
        var testFile = Path.Combine(sourceDir, "test_bz2.txt");
        await File.WriteAllTextAsync(testFile, "Hello BZip2 Archive Content 99999");

        var bz2Output = Path.Combine(_testWorkDir, "output.tar.bz2");
        var compResult = await _engine.CreateArchiveAsync(new CompressionRequest
        {
            OutputArchiveFilePath = bz2Output,
            Format = ArchiveFormat.BZip2,
            SourcePaths = [sourceDir]
        });

        Assert.True(compResult.Success, compResult.ErrorMessage);
        Assert.True(File.Exists(bz2Output));

        var info = await _engine.OpenArchiveAsync(bz2Output);
        Assert.Equal(ArchiveFormat.BZip2, info.Format);
        Assert.True(info.TotalFiles >= 1);

        var extractDir = Path.Combine(_testWorkDir, "extracted_bz2");
        var extResult = await _engine.ExtractAsync(new ExtractionRequest
        {
            ArchiveFilePath = bz2Output,
            DestinationDirectory = extractDir
        });

        Assert.True(extResult.Success, extResult.ErrorMessage);
        var extractedFiles = Directory.GetFiles(extractDir, "*.*", SearchOption.AllDirectories);
        Assert.True(extractedFiles.Length >= 1);
        var content = await File.ReadAllTextAsync(extractedFiles.First(f => f.EndsWith("test_bz2.txt", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal("Hello BZip2 Archive Content 99999", content);
    }

    [Fact]
    public async Task UnicodeAndSpecialCharacters_Roundtrip_PreservesExactNames()
    {
        var sourceDir = Path.Combine(_testWorkDir, "source_unicode");
        Directory.CreateDirectory(sourceDir);

        var file1 = Path.Combine(sourceDir, "arquivo_ação_coração.txt");
        var file2 = Path.Combine(sourceDir, "日本語_ファイル_test.txt");
        var file3 = Path.Combine(sourceDir, "über_größer_strauß.txt");

        await File.WriteAllTextAsync(file1, "Texto em português");
        await File.WriteAllTextAsync(file2, "日本語コンテンツ");
        await File.WriteAllTextAsync(file3, "Deutscher Text");

        var zipOutput = Path.Combine(_testWorkDir, "unicode.zip");
        var compResult = await _engine.CreateArchiveAsync(new CompressionRequest
        {
            OutputArchiveFilePath = zipOutput,
            Format = ArchiveFormat.Zip,
            SourcePaths = [sourceDir]
        });

        Assert.True(compResult.Success, compResult.ErrorMessage);

        var info = await _engine.OpenArchiveAsync(zipOutput);
        Assert.True(info.TotalFiles >= 3);

        var extractDir = Path.Combine(_testWorkDir, "extracted_unicode");
        var extResult = await _engine.ExtractAsync(new ExtractionRequest
        {
            ArchiveFilePath = zipOutput,
            DestinationDirectory = extractDir
        });

        Assert.True(extResult.Success, extResult.ErrorMessage);
        var extracted = Directory.GetFiles(extractDir, "*.*", SearchOption.AllDirectories);

        Assert.Contains(extracted, f => f.Contains("ação_coração", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(extracted, f => f.Contains("日本語_ファイル", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(extracted, f => f.Contains("über_größer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DeepNestedDirectories_15Levels_ExtractsProperly()
    {
        var current = Path.Combine(_testWorkDir, "deep_root");
        for (int i = 1; i <= 15; i++)
        {
            current = Path.Combine(current, $"lvl_{i:D2}");
        }
        Directory.CreateDirectory(current);

        var leafFile = Path.Combine(current, "leaf.txt");
        await File.WriteAllTextAsync(leafFile, "Deeply nested file at level 15");

        var zipOutput = Path.Combine(_testWorkDir, "deep.zip");
        var compResult = await _engine.CreateArchiveAsync(new CompressionRequest
        {
            OutputArchiveFilePath = zipOutput,
            Format = ArchiveFormat.Zip,
            SourcePaths = [Path.Combine(_testWorkDir, "deep_root")]
        });

        Assert.True(compResult.Success, compResult.ErrorMessage);

        var extractDir = Path.Combine(_testWorkDir, "extracted_deep");
        var extResult = await _engine.ExtractAsync(new ExtractionRequest
        {
            ArchiveFilePath = zipOutput,
            DestinationDirectory = extractDir
        });

        Assert.True(extResult.Success, extResult.ErrorMessage);
        var extractedLeaf = Directory.GetFiles(extractDir, "leaf.txt", SearchOption.AllDirectories);
        Assert.Single(extractedLeaf);
        Assert.Equal("Deeply nested file at level 15", await File.ReadAllTextAsync(extractedLeaf[0]));
    }

    [Fact]
    public async Task EmptyDirectoriesAndZeroByteFiles_HandledCorrectly()
    {
        var sourceDir = Path.Combine(_testWorkDir, "source_empty");
        var emptySubDir = Path.Combine(sourceDir, "empty_folder");
        Directory.CreateDirectory(emptySubDir);

        var zeroByteFile = Path.Combine(sourceDir, "zero.dat");
        await File.WriteAllBytesAsync(zeroByteFile, []);

        var zipOutput = Path.Combine(_testWorkDir, "empty.zip");
        var compResult = await _engine.CreateArchiveAsync(new CompressionRequest
        {
            OutputArchiveFilePath = zipOutput,
            Format = ArchiveFormat.Zip,
            SourcePaths = [sourceDir]
        });

        Assert.True(compResult.Success, compResult.DetailedError ?? compResult.ErrorMessage);

        var extractDir = Path.Combine(_testWorkDir, "extracted_empty");
        var extResult = await _engine.ExtractAsync(new ExtractionRequest
        {
            ArchiveFilePath = zipOutput,
            DestinationDirectory = extractDir
        });

        Assert.True(extResult.Success, extResult.ErrorMessage);
        var extractedZero = Directory.GetFiles(extractDir, "zero.dat", SearchOption.AllDirectories);
        Assert.Single(extractedZero);
        Assert.Equal(0, new FileInfo(extractedZero[0]).Length);
    }
}
