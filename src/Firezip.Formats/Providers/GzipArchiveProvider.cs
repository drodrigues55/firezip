using Firezip.Core.Models;
using SharpCompress.Common;
using SharpCompress.Writers.Tar;

namespace Firezip.Formats.Providers;

public class GzipArchiveProvider : ArchiveProviderBase
{
    public override ArchiveFormat Format => ArchiveFormat.GZip;

    public override bool CanHandle(string filePath)
    {
        return FormatDetector.DetectFormat(filePath) == ArchiveFormat.GZip;
    }

    public override async Task<OperationResult> CreateArchiveAsync(
        CompressionRequest request,
        CancellationToken cancellationToken = default)
    {
        var writerOptions = new TarWriterOptions(CompressionType.GZip, true)
        {
            ArchiveEncoding = new ArchiveEncoding { Default = System.Text.Encoding.UTF8 }
        };

        return await CommonCreateWithWriterAsync(request, ArchiveType.Tar, writerOptions, cancellationToken);
    }
}
