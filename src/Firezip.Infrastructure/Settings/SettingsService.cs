using System.Text.Json;
using Firezip.Core.Interfaces;
using Firezip.Core.Models;

namespace Firezip.Infrastructure.Settings;

public class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _settingsFilePath;
    private AppSettings _settings;

    public string DefaultExtractionFolder
    {
        get => _settings.DefaultExtractionFolder;
        set => _settings.DefaultExtractionFolder = value;
    }

    public bool OpenExtractedFolderAfterExtraction
    {
        get => _settings.OpenExtractedFolderAfterExtraction;
        set => _settings.OpenExtractedFolderAfterExtraction = value;
    }

    public bool KeepTaskProgressWindowOpen
    {
        get => _settings.KeepTaskProgressWindowOpen;
        set => _settings.KeepTaskProgressWindowOpen = value;
    }

    public bool DeleteArchiveAfterExtraction
    {
        get => _settings.DeleteArchiveAfterExtraction;
        set => _settings.DeleteArchiveAfterExtraction = value;
    }

    public string DoubleClickAction
    {
        get => _settings.DoubleClickAction;
        set => _settings.DoubleClickAction = value;
    }

    public bool ConfirmBeforeOverwriting
    {
        get => _settings.ConfirmBeforeOverwriting;
        set => _settings.ConfirmBeforeOverwriting = value;
    }

    public bool ConfirmBeforeDeleting
    {
        get => _settings.ConfirmBeforeDeleting;
        set => _settings.ConfirmBeforeDeleting = value;
    }

    public ArchiveFormat DefaultArchiveFormat
    {
        get => _settings.DefaultArchiveFormat;
        set => _settings.DefaultArchiveFormat = value;
    }

    public CompressionLevel DefaultCompressionLevel
    {
        get => _settings.DefaultCompressionLevel;
        set => _settings.DefaultCompressionLevel = value;
    }

    public string ThemeMode
    {
        get => _settings.ThemeMode;
        set => _settings.ThemeMode = value;
    }

    public string Language
    {
        get => _settings.Language;
        set => _settings.Language = value;
    }

    public string CustomTempDirectory
    {
        get => _settings.CustomTempDirectory;
        set => _settings.CustomTempDirectory = value;
    }

    public int MaxExtractionThreads
    {
        get => _settings.MaxExtractionThreads;
        set => _settings.MaxExtractionThreads = value;
    }

    public bool EnableContextMenu
    {
        get => _settings.EnableContextMenu;
        set => _settings.EnableContextMenu = value;
    }

    public bool UseCascadingContextMenu
    {
        get => _settings.UseCascadingContextMenu;
        set => _settings.UseCascadingContextMenu = value;
    }

    public bool AutoCloseTaskProgressWindow
    {
        get => _settings.AutoCloseTaskProgressWindow;
        set => _settings.AutoCloseTaskProgressWindow = value;
    }

    public bool NotifyOnTaskCompletion
    {
        get => _settings.NotifyOnTaskCompletion;
        set => _settings.NotifyOnTaskCompletion = value;
    }

    public IReadOnlyList<string> EnabledContextMenuCommands
    {
        get => _settings.EnabledContextMenuCommands;
        set => _settings.EnabledContextMenuCommands = value.ToList();
    }

    public IReadOnlyList<string> AssociatedExtensions
    {
        get => _settings.AssociatedExtensions;
        set => _settings.AssociatedExtensions = value.ToList();
    }

    public string LogLevel
    {
        get => _settings.LogLevel;
        set => _settings.LogLevel = value;
    }

    public bool AutoCheckUpdates
    {
        get => _settings.AutoCheckUpdates;
        set => _settings.AutoCheckUpdates = value;
    }

    public string UpdateCheckFrequency
    {
        get => _settings.UpdateCheckFrequency;
        set => _settings.UpdateCheckFrequency = value;
    }

    public DateTime? LastUpdateCheckTime
    {
        get => _settings.LastUpdateCheckTime;
        set => _settings.LastUpdateCheckTime = value;
    }

    public string LastUpdateCheckResult
    {
        get => _settings.LastUpdateCheckResult;
        set => _settings.LastUpdateCheckResult = value;
    }

    public SettingsService(string? customSettingsFilePath = null)
    {
        if (!string.IsNullOrEmpty(customSettingsFilePath))
        {
            _settingsFilePath = customSettingsFilePath;
        }
        else
        {
            // Isolated user preferences directory: %APPDATA%\Firezip (Roaming)
            // Preserved across complete uninstallation, clean reinstallation, and updates.
            var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(roamingAppData, "Firezip");
            Directory.CreateDirectory(folder);
            _settingsFilePath = Path.Combine(folder, "settings.json");

            // Migration check: If LocalApplicationData exists but Roaming doesn't, migrate it seamlessly
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var legacyFilePath = Path.Combine(localAppData, "Firezip", "settings.json");
            if (!File.Exists(_settingsFilePath) && File.Exists(legacyFilePath))
            {
                try
                {
                    File.Copy(legacyFilePath, _settingsFilePath, true);
                }
                catch
                {
                    // Fallback ignored
                }
            }
        }

        _settings = new AppSettings();
    }

    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = await File.ReadAllTextAsync(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded != null)
                {
                    _settings = loaded;
                }
            }
        }
        catch
        {
            _settings = new AppSettings();
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            var folder = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var json = JsonSerializer.Serialize(_settings, JsonOptions);
            var tempFile = _settingsFilePath + ".tmp";
            await File.WriteAllTextAsync(tempFile, json);
            File.Move(tempFile, _settingsFilePath, true);
        }
        catch
        {
            // Ignore non-fatal settings save errors
        }
    }
}
