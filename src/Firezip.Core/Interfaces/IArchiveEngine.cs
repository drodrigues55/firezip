using Firezip.Core.Models;

namespace Firezip.Core.Interfaces;

/// <summary>
/// High-level engine coordinating format detection and provider delegation.
/// </summary>
public interface IArchiveEngine
{
    IReadOnlyList<IArchiveProvider> Providers { get; }

    /// <summary>
    /// Detects the format of the given file path by inspection.
    /// </summary>
    ArchiveFormat DetectFormat(string filePath);

    /// <summary>
    /// Gets the provider responsible for the specified format or file path.
    /// </summary>
    IArchiveProvider GetProvider(string filePath);

    /// <summary>
    /// Gets the provider for a specific format.
    /// </summary>
    IArchiveProvider GetProvider(ArchiveFormat format);

    /// <summary>
    /// Opens an archive file and returns its metadata and entry tree.
    /// </summary>
    Task<ArchiveInfo> OpenArchiveAsync(string filePath, string? password = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes extraction request with progress and conflict handling.
    /// </summary>
    Task<OperationResult> ExtractAsync(ExtractionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes compression request.
    /// </summary>
    Task<OperationResult> CreateArchiveAsync(CompressionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests archive integrity.
    /// </summary>
    Task<OperationResult> TestArchiveAsync(TestArchiveRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes entries from an archive.
    /// </summary>
    Task<OperationResult> DeleteEntriesAsync(string archivePath, IReadOnlyList<string> entryPaths, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds files/folders to an archive.
    /// </summary>
    Task<OperationResult> AddEntriesAsync(string archivePath, IReadOnlyList<string> sourceFilePaths, string? targetSubfolder = null, CancellationToken cancellationToken = default);
}
