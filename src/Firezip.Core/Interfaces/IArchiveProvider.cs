using Firezip.Core.Models;

namespace Firezip.Core.Interfaces;

/// <summary>
/// Common abstraction for format-specific archive providers.
/// </summary>
public interface IArchiveProvider
{
    ArchiveFormat Format { get; }

    /// <summary>
    /// Checks if this provider can handle the given archive (by extension and/or stream signature).
    /// </summary>
    bool CanHandle(string filePath);

    /// <summary>
    /// Opens and enumerates archive entries without full extraction.
    /// </summary>
    Task<ArchiveInfo> OpenArchiveAsync(string filePath, string? password = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts entries according to the request.
    /// </summary>
    Task<OperationResult> ExtractAsync(ExtractionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new archive from the specified source paths.
    /// </summary>
    Task<OperationResult> CreateArchiveAsync(CompressionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests the integrity of the archive.
    /// </summary>
    Task<OperationResult> TestArchiveAsync(TestArchiveRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes specified entries from an archive if supported by the provider.
    /// </summary>
    Task<OperationResult> DeleteEntriesAsync(string archivePath, IReadOnlyList<string> entryPaths, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds files/folders to an existing archive.
    /// </summary>
    Task<OperationResult> AddEntriesAsync(string archivePath, IReadOnlyList<string> sourceFilePaths, string? targetSubfolder = null, CancellationToken cancellationToken = default);
}
