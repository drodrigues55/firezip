using Firezip.Core.Models;

namespace Firezip.Formats.Providers;

public class RarArchiveProvider : ArchiveProviderBase
{
    public override ArchiveFormat Format => ArchiveFormat.Rar;

    public override bool CanHandle(string filePath)
    {
        return FormatDetector.DetectFormat(filePath) == ArchiveFormat.Rar;
    }

    public override Task<OperationResult> CreateArchiveAsync(
        CompressionRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(OperationResult.Failed("RAR archive creation is proprietary and not supported. Please select 7Z or ZIP for archive creation."));
    }

    public override Task<OperationResult> DeleteEntriesAsync(
        string archivePath,
        IReadOnlyList<string> entryPaths,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(OperationResult.Failed("Modifying RAR archives is not supported because RAR creation is proprietary."));
    }

    public override Task<OperationResult> AddEntriesAsync(
        string archivePath,
        IReadOnlyList<string> sourceFilePaths,
        string? targetSubfolder = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(OperationResult.Failed("Modifying RAR archives is not supported because RAR creation is proprietary."));
    }
}
