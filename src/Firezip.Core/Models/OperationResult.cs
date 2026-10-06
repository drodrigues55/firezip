namespace Firezip.Core.Models;

/// <summary>
/// Result of an extraction, compression, or test operation.
/// </summary>
public record OperationResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? DetailedError { get; init; }
    public bool IsCancelled { get; init; }
    public int FilesProcessed { get; init; }
    public int FilesSkipped { get; init; }
    public long BytesProcessed { get; init; }
    public TimeSpan ElapsedTime { get; init; }
    public IReadOnlyList<string> CorruptedEntries { get; init; } = [];

    public static OperationResult Succeeded(int filesProcessed, long bytesProcessed, TimeSpan elapsed, int filesSkipped = 0) =>
        new()
        {
            Success = true,
            FilesProcessed = filesProcessed,
            FilesSkipped = filesSkipped,
            BytesProcessed = bytesProcessed,
            ElapsedTime = elapsed
        };

    public static OperationResult Cancelled(int filesProcessed = 0, long bytesProcessed = 0, TimeSpan elapsed = default) =>
        new()
        {
            Success = false,
            IsCancelled = true,
            ErrorMessage = "Operation was cancelled by the user.",
            FilesProcessed = filesProcessed,
            BytesProcessed = bytesProcessed,
            ElapsedTime = elapsed
        };

    public static OperationResult Failed(string message, string? details = null, int filesProcessed = 0, long bytesProcessed = 0) =>
        new()
        {
            Success = false,
            ErrorMessage = message,
            DetailedError = details,
            FilesProcessed = filesProcessed,
            BytesProcessed = bytesProcessed
        };
}
