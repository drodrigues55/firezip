using Firezip.Core.Models;
using Firezip.Formats;
using Xunit;

namespace Firezip.Tests;

public class FormatDetectionTests
{
    [Theory]
    [InlineData("sample.zip", ArchiveFormat.Zip)]
    [InlineData("backup.7z", ArchiveFormat.SevenZip)]
    [InlineData("archive.tar", ArchiveFormat.Tar)]
    [InlineData("data.tar.gz", ArchiveFormat.GZip)]
    [InlineData("data.tgz", ArchiveFormat.GZip)]
    [InlineData("data.tar.bz2", ArchiveFormat.BZip2)]
    [InlineData("data.tbz2", ArchiveFormat.BZip2)]
    [InlineData("data.tar.xz", ArchiveFormat.Xz)]
    [InlineData("data.txz", ArchiveFormat.Xz)]
    [InlineData("movie.rar", ArchiveFormat.Rar)]
    [InlineData("disc.iso", ArchiveFormat.Iso)]
    [InlineData("installer.cab", ArchiveFormat.Cab)]
    public void DetectByExtension_ReturnsExpectedFormat(string fileName, ArchiveFormat expectedFormat)
    {
        var detected = FormatDetector.DetectByExtension(fileName);
        Assert.Equal(expectedFormat, detected);
    }

    [Fact]
    public void DetectFormat_ZipMagicBytes_DetectedAccurately()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            // Standard ZIP magic bytes: PK\x03\x04
            File.WriteAllBytes(tempFile, [0x50, 0x4B, 0x03, 0x04, 0x00, 0x00]);
            var format = FormatDetector.DetectFormat(tempFile);
            Assert.Equal(ArchiveFormat.Zip, format);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void DetectFormat_SevenZipMagicBytes_DetectedAccurately()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            // Standard 7Z magic bytes: 7z\xBC\xAF\x27\x1C
            File.WriteAllBytes(tempFile, [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C, 0x00, 0x00]);
            var format = FormatDetector.DetectFormat(tempFile);
            Assert.Equal(ArchiveFormat.SevenZip, format);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void DetectFormat_RarMagicBytes_DetectedAccurately()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            // RAR5 magic bytes: Rar!\x1A\x07\x01\x00
            File.WriteAllBytes(tempFile, [0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x01, 0x00]);
            var format = FormatDetector.DetectFormat(tempFile);
            Assert.Equal(ArchiveFormat.Rar, format);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
