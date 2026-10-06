using Firezip.Core.Models;

namespace Firezip.Infrastructure.Settings;

public class AppSettings
{
    public string DefaultExtractionFolder { get; set; } = string.Empty;
    public bool OpenExtractedFolderAfterExtraction { get; set; } = true;
    public bool KeepTaskProgressWindowOpen { get; set; } = false;
    public bool DeleteArchiveAfterExtraction { get; set; } = false;
    public string DoubleClickAction { get; set; } = "OpenInFirezip";
    public bool ConfirmBeforeOverwriting { get; set; } = true;
    public bool ConfirmBeforeDeleting { get; set; } = true;
    public ArchiveFormat DefaultArchiveFormat { get; set; } = ArchiveFormat.Zip;
    public CompressionLevel DefaultCompressionLevel { get; set; } = CompressionLevel.Normal;
    public string ThemeMode { get; set; } = "System"; // "System", "Light", "Dark"
    public string Language { get; set; } = "System"; // "System", "en-US", "pt-BR"
    public string CustomTempDirectory { get; set; } = string.Empty;
    public int MaxExtractionThreads { get; set; } = Environment.ProcessorCount;
    public bool EnableContextMenu { get; set; } = true;
    public bool UseCascadingContextMenu { get; set; } = false;
    public bool AutoCloseTaskProgressWindow { get; set; } = true;
    public bool NotifyOnTaskCompletion { get; set; } = true;
    public List<string> EnabledContextMenuCommands { get; set; } =
    [
        "OpenWith",
        "ExtractHere",
        "ExtractToFolder",
        "ExtractTo",
        "CompressToZip",
        "CompressTo7z"
    ];
    public List<string> AssociatedExtensions { get; set; } =
    [
        ".zip",
        ".zipx",
        ".jar",
        ".apk",
        ".7z",
        ".rar",
        ".tar",
        ".gz",
        ".bz2",
        ".xz",
        ".tgz",
        ".tbz2",
        ".txz"
    ];
    public string LogLevel { get; set; } = "Information";
    public bool AutoCheckUpdates { get; set; } = true;
    public string UpdateCheckFrequency { get; set; } = "Daily";
    public DateTime? LastUpdateCheckTime { get; set; }
    public string LastUpdateCheckResult { get; set; } = string.Empty;
}
