using Firezip.Core.Models;
using SharpCompress.Common;
using SharpCompress.Writers.Tar;

namespace Firezip.Formats.Providers;

public class BZip2ArchiveProvider : ArchiveProviderBase
{
    public override ArchiveFormat Format => ArchiveFormat.BZip2;

    public override bool CanHandle(string filePath)
    {
        return FormatDetector.DetectFormat(filePath) == ArchiveFormat.BZip2;
    }

    public override async Task<OperationResult> CreateArchiveAsync(
        CompressionRequest request,
        CancellationToken cancellationToken = default)
    {
        var writerOptions = new TarWriterOptions(CompressionType.BZip2, true)
        {
            ArchiveEncoding = new ArchiveEncoding { Default = System.Text.Encoding.UTF8 }
        };

        return await CommonCreateWithWriterAsync(request, ArchiveType.Tar, writerOptions, cancellationToken);
    }
}
