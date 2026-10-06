namespace Firezip.Core.Models;

public record ExtractionRequest
{
    public required string ArchiveFilePath { get; init; }
    public required string DestinationDirectory { get; init; }
    public IReadOnlyList<ArchiveEntry>? EntriesToExtract { get; init; }
    public string? Password { get; init; }
    public ConflictPolicy DefaultConflictPolicy { get; init; } = ConflictPolicy.AskUser;
    public Func<string, Task<ConflictResolution>>? ConflictCallback { get; init; }
    public IProgress<OperationProgress>? Progress { get; init; }
}

public record CompressionRequest
{
    public required string OutputArchiveFilePath { get; init; }
    public required ArchiveFormat Format { get; init; }
    public required IReadOnlyList<string> SourcePaths { get; init; }
    public CompressionOptions Options { get; init; } = CompressionOptions.Default;
    public IProgress<OperationProgress>? Progress { get; init; }
}

public record TestArchiveRequest
{
    public required string ArchiveFilePath { get; init; }
    public string? Password { get; init; }
    public IProgress<OperationProgress>? Progress { get; init; }
}
