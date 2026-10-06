using Firezip.Core.Models;

namespace Firezip.Formats.Providers;

public class SevenZipArchiveProvider : ArchiveProviderBase
{
    public override ArchiveFormat Format => ArchiveFormat.SevenZip;

    public override bool CanHandle(string filePath)
    {
        return FormatDetector.DetectFormat(filePath) == ArchiveFormat.SevenZip;
    }

    public override Task<OperationResult> CreateArchiveAsync(
        CompressionRequest request,
        CancellationToken cancellationToken = default)
    {
        return SevenZipCompressionAdapter.CreateArchiveAsync(request, Format, cancellationToken);
    }
}
