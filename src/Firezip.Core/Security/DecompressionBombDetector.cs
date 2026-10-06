namespace Firezip.Core.Security;

/// <summary>
/// Monitors suspicious archive metadata and caps actual output while streaming.
/// </summary>
public class DecompressionBombDetector
{
    /// <summary>200:1 expansion ratio — suspicious for large data.</summary>
    public const double DefaultSuspiciousRatio = 200.0;

    /// <summary>No fixed per-file cap by default, preserving support for arbitrarily large archives.</summary>
    public const long DefaultMaxUncompressedEntryBytes = long.MaxValue;

    /// <summary>
    /// Minimum uncompressed size (10 MB) before the ratio check is applied.
    /// Small files can legitimately have extreme ratios (e.g. a 1-byte text file compressed to 1 byte).
    /// </summary>
    private const long MinSizeForRatioCheck = 10L * 1024 * 1024;

    /// <summary>
    private readonly double _ratioThreshold;
    private readonly long _maxUncompressedEntryBytes;

    // Actual emitted bytes for the current entry.
    private long _totalUncompressedWritten;

    public DecompressionBombDetector(
        double ratioThreshold = DefaultSuspiciousRatio,
        long maxUncompressedEntryBytes = DefaultMaxUncompressedEntryBytes)
    {
        _ratioThreshold = ratioThreshold;
        _maxUncompressedEntryBytes = maxUncompressedEntryBytes;
    }

    /// <summary>
    /// Pre-extraction check: inspects static archive entry metadata (sizes stored in the archive header)
    /// before any data is actually read. Returns <c>true</c> if the entry looks suspicious.
    /// </summary>
    /// <param name="uncompressedSize">Uncompressed size reported in the archive header.</param>
    /// <param name="compressedSize">Compressed size reported in the archive header.</param>
    /// <param name="reason">Human-readable explanation when suspicious.</param>
    public bool IsEntrySuspicious(long uncompressedSize, long compressedSize, out string reason)
    {
        reason = string.Empty;

        if (_maxUncompressedEntryBytes != long.MaxValue && uncompressedSize > _maxUncompressedEntryBytes)
        {
            reason = $"Uncompressed size exceeds the configured limit ({_maxUncompressedEntryBytes} bytes).";
            return true;
        }

        // Apply ratio check only when the file is large enough that a real ratio can be measured.
        // compressedSize must be > 0 to avoid division-by-zero (some formats store 0 for directories).
        if (compressedSize > 0 && uncompressedSize >= MinSizeForRatioCheck)
        {
            double ratio = (double)uncompressedSize / compressedSize;
            if (ratio > _ratioThreshold)
            {
                reason = $"Abnormally high compression ratio ({ratio:F1}:1). Possible decompression bomb.";
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Tracks the actual output produced by one entry. The archive library does not expose
    /// reliable compressed-byte deltas per entry, so compression ratios are checked from
    /// archive metadata; streaming verifies declared size and any explicitly configured cap.
    /// </summary>
    /// <param name="compressedDelta">Compressed input bytes for this chunk, when available.</param>
    /// <param name="uncompressedDelta">Bytes written to the output stream this chunk.</param>
    /// <param name="expectedEntrySize">Declared size, or a negative value when unavailable.</param>
    public void TrackProgress(long compressedDelta, long uncompressedDelta, long expectedEntrySize = -1)
    {
        if (compressedDelta < 0 || uncompressedDelta < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(uncompressedDelta), "Byte counts cannot be negative.");
        }

        _totalUncompressedWritten = checked(_totalUncompressedWritten + uncompressedDelta);

        if (expectedEntrySize >= 0 && _totalUncompressedWritten > expectedEntrySize)
        {
            throw new SecurityValidationException(
                $"Entry produced more data than its declared size ({expectedEntrySize} bytes).",
                "STREAM",
                "DecompressionBomb");
        }

        if (_maxUncompressedEntryBytes != long.MaxValue && _totalUncompressedWritten > _maxUncompressedEntryBytes)
            throw new SecurityValidationException(
                $"Entry exceeded the configured extracted size limit ({_maxUncompressedEntryBytes} bytes).",
                "STREAM",
                "DecompressionBomb");
    }

    /// <summary>Resets the output count. Use between separate archive entries.</summary>
    public void Reset()
    {
        _totalUncompressedWritten = 0;
    }
}
