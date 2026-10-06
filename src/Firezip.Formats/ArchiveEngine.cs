using Firezip.Core.Interfaces;
using Firezip.Core.Models;
using Firezip.Formats.Providers;

namespace Firezip.Formats;

public class ArchiveEngine : IArchiveEngine
{
    private readonly List<IArchiveProvider> _providers;

    public IReadOnlyList<IArchiveProvider> Providers => _providers.AsReadOnly();

    public ArchiveEngine(IEnumerable<IArchiveProvider>? customProviders = null)
    {
        var configuredProviders = customProviders?.ToList();
        _providers = configuredProviders is { Count: > 0 } ? configuredProviders : new List<IArchiveProvider>
        {
            new ZipArchiveProvider(),
            new SevenZipArchiveProvider(),
            new TarArchiveProvider(),
            new GzipArchiveProvider(),
            new BZip2ArchiveProvider(),
            new XzArchiveProvider(),
            new RarArchiveProvider()
        };
    }

    public ArchiveFormat DetectFormat(string filePath)
    {
        return FormatDetector.DetectFormat(filePath);
    }

    public IArchiveProvider GetProvider(string filePath)
    {
        var format = DetectFormat(filePath);
        var provider = _providers.FirstOrDefault(p => p.Format == format && p.CanHandle(filePath))
                       ?? _providers.FirstOrDefault(p => p.Format == format)
                       ?? _providers.FirstOrDefault(p => p.CanHandle(filePath));

        if (provider == null)
        {
            throw new NotSupportedException($"The archive file format '{Path.GetExtension(filePath)}' is not supported.");
        }

        return provider;
    }

    public IArchiveProvider GetProvider(ArchiveFormat format)
    {
        var provider = _providers.FirstOrDefault(p => p.Format == format);
        if (provider == null)
        {
            throw new NotSupportedException($"No provider registered for format '{format}'.");
        }
        return provider;
    }

    public Task<ArchiveInfo> OpenArchiveAsync(string filePath, string? password = null, CancellationToken cancellationToken = default)
    {
        var provider = GetProvider(filePath);
        return provider.OpenArchiveAsync(filePath, password, cancellationToken);
    }

    public Task<OperationResult> ExtractAsync(ExtractionRequest request, CancellationToken cancellationToken = default)
    {
        var provider = GetProvider(request.ArchiveFilePath);
        return provider.ExtractAsync(request, cancellationToken);
    }

    public Task<OperationResult> CreateArchiveAsync(CompressionRequest request, CancellationToken cancellationToken = default)
    {
        var provider = GetProvider(request.Format);
        return provider.CreateArchiveAsync(request, cancellationToken);
    }

    public Task<OperationResult> TestArchiveAsync(TestArchiveRequest request, CancellationToken cancellationToken = default)
    {
        var provider = GetProvider(request.ArchiveFilePath);
        return provider.TestArchiveAsync(request, cancellationToken);
    }

    public Task<OperationResult> DeleteEntriesAsync(string archivePath, IReadOnlyList<string> entryPaths, CancellationToken cancellationToken = default)
    {
        var provider = GetProvider(archivePath);
        return provider.DeleteEntriesAsync(archivePath, entryPaths, cancellationToken);
    }

    public Task<OperationResult> AddEntriesAsync(string archivePath, IReadOnlyList<string> sourceFilePaths, string? targetSubfolder = null, CancellationToken cancellationToken = default)
    {
        var provider = GetProvider(archivePath);
        return provider.AddEntriesAsync(archivePath, sourceFilePaths, targetSubfolder, cancellationToken);
    }
}
