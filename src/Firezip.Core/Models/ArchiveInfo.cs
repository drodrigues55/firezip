namespace Firezip.Core.Models;

/// <summary>
/// Represents full metadata and entry list for an opened archive.
/// </summary>
public record ArchiveInfo
{
    public required string FilePath { get; init; }
    public required ArchiveFormat Format { get; init; }
    private IReadOnlyList<ArchiveEntry> _entries = [];
    public IReadOnlyList<ArchiveEntry> Entries
    {
        get => _entries;
        init
        {
            _entries = value;
            int files = 0;
            int dirs = 0;
            long uncompressed = 0;
            long compressed = 0;
            foreach (var entry in value)
            {
                if (entry.IsDirectory)
                {
                    dirs++;
                }
                else
                {
                    files++;
                    uncompressed += entry.Size;
                    compressed += entry.CompressedSize;
                }
            }
            TotalFiles = files;
            TotalDirectories = dirs;
            TotalUncompressedSize = uncompressed;
            TotalCompressedSize = compressed;
        }
    }
    public bool IsEncrypted { get; init; }
    public string? Comment { get; init; }
    public bool IsSolid { get; init; }

    public int TotalEntries => _entries.Count;
    public int TotalFiles { get; private init; }
    public int TotalDirectories { get; private init; }
    public long TotalUncompressedSize { get; private init; }
    public long TotalCompressedSize { get; private init; }

    public double? OverallCompressionRatio =>
        TotalUncompressedSize > 0 ? Math.Clamp((double)TotalCompressedSize / TotalUncompressedSize * 100.0, 0.0, 999.0) : null;

    private ILookup<string, ArchiveEntry>? _directoryLookup;
    private Dictionary<string, ArchiveEntry>? _entryByPathLookup;

    /// <summary>
    /// Gets a specific entry by its normalized full path in O(1) time.
    /// </summary>
    public ArchiveEntry? GetEntryByPath(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath)) return null;
        var normalized = fullPath.Replace('\\', '/').Trim('/');
        _entryByPathLookup ??= _entries
            .GroupBy(e => e.FullPath.Replace('\\', '/').Trim('/'), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        return _entryByPathLookup.TryGetValue(normalized, out var entry) ? entry : null;
    }

    /// <summary>
    /// Returns entries that reside directly inside the specified folder path.
    /// Use empty string for root entries.
    /// </summary>
    public IEnumerable<ArchiveEntry> GetEntriesInDirectory(string folderPath)
    {
        var normalized = folderPath.Replace('\\', '/').Trim('/');
        _directoryLookup ??= _entries.ToLookup(e => e.ParentDirectory.Trim('/'), StringComparer.OrdinalIgnoreCase);
        return _directoryLookup[normalized];
    }

    /// <summary>
    /// Searches entries by name or path match.
    /// </summary>
    public IEnumerable<ArchiveEntry> SearchEntries(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Entries;

        return Entries.Where(e =>
            e.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            e.FullPath.Contains(query, StringComparison.OrdinalIgnoreCase));
    }
}
