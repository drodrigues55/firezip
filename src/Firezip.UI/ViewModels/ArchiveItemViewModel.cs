using CommunityToolkit.Mvvm.ComponentModel;
using Firezip.Core.Models;

namespace Firezip.UI.ViewModels;

public partial class ArchiveItemViewModel : ObservableObject
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public bool IsDirectory { get; init; }
    public bool IsParentFolderLink { get; init; }
    public long Size { get; init; }
    public long CompressedSize { get; init; }
    public DateTime? ModifiedDate { get; init; }
    public double? CompressionRatio { get; init; }
    public string Crc { get; init; } = string.Empty;
    public string Attributes { get; init; } = string.Empty;

    public string FormattedSize => IsDirectory ? string.Empty : ArchiveEntry.FormatBytes(Size);
    public string FormattedCompressedSize => IsDirectory ? string.Empty : ArchiveEntry.FormatBytes(CompressedSize);
    public string FormattedModified => ModifiedDate?.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    public string FormattedRatio => CompressionRatio.HasValue && !IsDirectory ? $"{CompressionRatio.Value:F1}%" : string.Empty;

    public string IconGlyph => IsParentFolderLink
        ? "\uE751" // Up arrow
        : (IsDirectory ? "\uE8B7" : GetFileIconGlyph(Name));

    private static string GetFileIconGlyph(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".zip" or ".7z" or ".rar" or ".tar" or ".gz" => "\uF012", // Archive icon
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".svg" or ".webp" => "\uEB9F", // Image icon
            ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" => "\uE714", // Video icon
            ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" => "\uE8D6", // Audio icon
            ".txt" or ".md" or ".log" or ".rtf" => "\uE8C4", // Document text
            ".pdf" => "\uEA90", // PDF icon
            ".exe" or ".msi" or ".bat" or ".cmd" or ".ps1" => "\uE756", // Executable
            ".cs" or ".cpp" or ".h" or ".js" or ".ts" or ".html" or ".css" or ".json" or ".xml" => "\uE943", // Code
            _ => "\uE8A5" // Default file
        };
    }

    public static ArchiveItemViewModel FromEntry(ArchiveEntry entry)
    {
        return new ArchiveItemViewModel
        {
            Name = entry.Name,
            FullPath = entry.FullPath,
            IsDirectory = entry.IsDirectory,
            Size = entry.Size,
            CompressedSize = entry.CompressedSize,
            ModifiedDate = entry.ModifiedDate,
            CompressionRatio = entry.CompressionRatio,
            Crc = entry.FormattedCrc,
            Attributes = entry.Attributes ?? string.Empty
        };
    }

    public static ArchiveItemViewModel CreateParentFolderLink(string currentDirectory)
    {
        var normalized = currentDirectory.Replace('\\', '/').TrimEnd('/');
        var lastSlash = normalized.LastIndexOf('/');
        var parentPath = lastSlash >= 0 ? normalized[..lastSlash] : string.Empty;

        return new ArchiveItemViewModel
        {
            Name = "..",
            FullPath = parentPath,
            IsDirectory = true,
            IsParentFolderLink = true
        };
    }
}
