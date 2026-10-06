using Firezip.Core.Models;

namespace Firezip.Formats.Providers;

public class XzArchiveProvider : ArchiveProviderBase
{
    public override ArchiveFormat Format => ArchiveFormat.Xz;

    public override bool CanHandle(string filePath)
    {
        return FormatDetector.DetectFormat(filePath) == ArchiveFormat.Xz;
    }

    public override Task<OperationResult> CreateArchiveAsync(
        CompressionRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(OperationResult.Failed("Creating XZ archives is not directly supported. Please choose 7Z or ZIP."));
    }
}
