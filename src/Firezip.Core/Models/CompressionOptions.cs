namespace Firezip.Core.Models;

public enum CompressionLevel
{
    Store = 0,
    Fast = 1,
    Normal = 2,
    Maximum = 3,
    Ultra = 4
}

public record CompressionOptions
{
    public CompressionLevel Level { get; init; } = CompressionLevel.Normal;
    public string? Password { get; init; }
    public bool EncryptHeader { get; init; }
    public bool SplitArchive { get; init; }
    public long? VolumeSizeBytes { get; init; }
    public bool PreserveTimestamps { get; init; } = true;
    public IReadOnlyList<string> ExcludePatterns { get; init; } = [];

    public static CompressionOptions Default => new();
}
