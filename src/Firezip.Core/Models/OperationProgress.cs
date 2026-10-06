namespace Firezip.Core.Models;

/// <summary>
/// Real-time progress data emitted during extraction, compression, or integrity testing.
/// </summary>
public record OperationProgress
{
    public long BytesProcessed { get; init; }
    public long TotalBytes { get; init; }
    public int FilesProcessed { get; init; }
    public int TotalFiles { get; init; }
    public string CurrentItemName { get; init; } = string.Empty;
    public double BytesPerSecond { get; init; }
    public TimeSpan? EstimatedTimeRemaining { get; init; }
    public string OperationPhase { get; init; } = string.Empty;
    public long ItemBytesProcessed { get; init; }
    public long ItemTotalBytes { get; init; }
    public long CompressedBytes { get; init; }

    public double ItemPercentage =>
        ItemTotalBytes > 0
            ? Math.Clamp((double)ItemBytesProcessed / ItemTotalBytes * 100.0, 0.0, 100.0)
            : (ItemBytesProcessed > 0 ? 100.0 : 0.0);

    public double CompressionRatio =>
        TotalBytes > 0 && CompressedBytes > 0
            ? Math.Clamp((double)CompressedBytes / TotalBytes * 100.0, 0.0, 200.0)
            : 0.0;

    public double Percentage
    {
        get
        {
            if (TotalBytes > 0)
                return Math.Clamp((double)BytesProcessed / TotalBytes * 100.0, 0.0, 100.0);
            if (TotalFiles > 0)
                return Math.Clamp((double)FilesProcessed / TotalFiles * 100.0, 0.0, 100.0);
            return 0.0;
        }
    }

    public string FormattedSpeed => $"{ArchiveEntry.FormatBytes((long)BytesPerSecond)}/s";

    public string FormattedRemainingTime =>
        EstimatedTimeRemaining.HasValue
            ? (EstimatedTimeRemaining.Value.TotalHours >= 1
                ? $"{EstimatedTimeRemaining.Value:hh\\:mm\\:ss}"
                : $"{EstimatedTimeRemaining.Value:mm\\:ss}")
            : "Calculating...";
}
