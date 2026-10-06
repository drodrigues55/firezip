using Firezip.Core.Models;

namespace Firezip.Core.Interfaces;

public interface ISettingsService
{
    string DefaultExtractionFolder { get; set; }
    bool OpenExtractedFolderAfterExtraction { get; set; }
    bool ConfirmBeforeOverwriting { get; set; }
    bool ConfirmBeforeDeleting { get; set; }
    ArchiveFormat DefaultArchiveFormat { get; set; }
    CompressionLevel DefaultCompressionLevel { get; set; }
    string ThemeMode { get; set; } // "System", "Light", "Dark"
    string Language { get; set; } // "System", "en-US", "pt-BR"
    string CustomTempDirectory { get; set; }
    int MaxExtractionThreads { get; set; }
    bool EnableContextMenu { get; set; }
    bool UseCascadingContextMenu { get; set; }
    bool AutoCloseTaskProgressWindow { get; set; }
    bool NotifyOnTaskCompletion { get; set; }
    IReadOnlyList<string> EnabledContextMenuCommands { get; set; }
    IReadOnlyList<string> AssociatedExtensions { get; set; }
    string LogLevel { get; set; }
    bool AutoCheckUpdates { get; set; }
    string UpdateCheckFrequency { get; set; } // "Daily", "Weekly", "Monthly"
    DateTime? LastUpdateCheckTime { get; set; }
    string LastUpdateCheckResult { get; set; }

    Task LoadAsync();
    Task SaveAsync();
}
