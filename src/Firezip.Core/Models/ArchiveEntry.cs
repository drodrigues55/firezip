namespace Firezip.Core.Models;

/// <summary>
/// Represents a single item (file or directory) within an archive.
/// </summary>
public record ArchiveEntry
{
    public required string FullPath { get; init; }
    public required string Name { get; init; }
    public long Size { get; init; }
    public long CompressedSize { get; init; }
    public DateTime? ModifiedDate { get; init; }
    public bool IsDirectory { get; init; }
    public bool IsEncrypted { get; init; }
    public string? Attributes { get; init; }
    public long? Crc { get; init; }
    public string FormattedCrc => Crc.HasValue ? $"{Crc.Value:X8}" : string.Empty;
    public string? Comment { get; init; }

    private string? _parentDirectory;

    /// <summary>
    /// Gets the parent directory path within the archive, normalized with forward slashes without trailing slash.
    /// Root items return string.Empty.
    /// </summary>
    public string ParentDirectory
    {
        get
        {
            if (_parentDirectory != null) return _parentDirectory;
            var normalized = FullPath.Replace('\\', '/').TrimEnd('/');
            var lastSlash = normalized.LastIndexOf('/');
            _parentDirectory = lastSlash >= 0 ? normalized[..lastSlash] : string.Empty;
            return _parentDirectory;
        }
        init => _parentDirectory = value;
    }

    /// <summary>
    /// Compression ratio as percentage (0% to 100%), or null if uncompressed size is 0.
    /// </summary>
    public double? CompressionRatio =>
        Size > 0 && CompressedSize >= 0 ? Math.Clamp((double)CompressedSize / Size * 100.0, 0.0, 999.0) : null;

    /// <summary>
    /// Formatted human-readable file size (e.g., "1.4 MB", "512 KB").
    /// </summary>
    public string FormattedSize => IsDirectory ? string.Empty : FormatBytes(Size);

    /// <summary>
    /// Formatted human-readable compressed size.
    /// </summary>
    public string FormattedCompressedSize => IsDirectory ? string.Empty : FormatBytes(CompressedSize);

    public static string FormatBytes(long bytes)
    {
        if (bytes < 0) return "0 B";
        string[] suffixes = ["B", "KB", "MB", "GB", "TB", "PB"];
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024m) >= 1 && counter < suffixes.Length - 1)
        {
            number /= 1024m;
            counter++;
        }
        return $"{number:0.##} {suffixes[counter]}";
    }
}
