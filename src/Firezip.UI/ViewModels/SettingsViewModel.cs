using CommunityToolkit.Mvvm.ComponentModel;
using Firezip.Core.Interfaces;
using Firezip.Core.Models;
using Firezip.Windows.Shell;

namespace Firezip.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ILoggingService _loggingService;
    private readonly ITaskSchedulerService _taskSchedulerService;
    private readonly IUpdateCoordinator _updateCoordinator;
    private readonly ILocalizationService? _localizationService;

    [ObservableProperty]
    private string _language = "System";

    [ObservableProperty]
    private string _defaultExtractionFolder;

    [ObservableProperty]
    private bool _openFolderAfterExtraction;

    [ObservableProperty]
    private bool _keepTaskProgressWindowOpen;

    [ObservableProperty]
    private bool _deleteArchiveAfterExtraction;

    [ObservableProperty]
    private string _doubleClickAction = "OpenInFirezip";

    [ObservableProperty]
    private bool _confirmBeforeOverwriting;

    [ObservableProperty]
    private bool _confirmBeforeDeleting;

    [ObservableProperty]
    private ArchiveFormat _defaultArchiveFormat;

    [ObservableProperty]
    private CompressionLevel _defaultCompressionLevel;

    [ObservableProperty]
    private string _themeMode;

    [ObservableProperty]
    private bool _enableContextMenu;

    [ObservableProperty]
    private bool _useCascadingContextMenu;

    [ObservableProperty]
    private bool _autoCloseTaskProgressWindow;

    [ObservableProperty]
    private bool _notifyOnTaskCompletion;

    [ObservableProperty]
    private bool _enableOpenWith;

    [ObservableProperty]
    private bool _enableExtractHere;

    [ObservableProperty]
    private bool _enableExtractToFolder;

    [ObservableProperty]
    private bool _enableExtractTo;

    [ObservableProperty]
    private bool _enableCompressToZip;

    [ObservableProperty]
    private bool _enableCompressTo7z;

    [ObservableProperty]
    private bool _autoCheckUpdates;

    [ObservableProperty]
    private string _updateCheckFrequency;

    [ObservableProperty]
    private string _currentVersion = "1.0.0";

    [ObservableProperty]
    private string _lastCheckDisplay = "Nunca";

    [ObservableProperty]
    private string _updateStatusMessage = string.Empty;

    [ObservableProperty]
    private bool _isCheckingForUpdates;

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private string _latestVersionAvailable = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public SettingsViewModel(
        ISettingsService settingsService,
        ILoggingService loggingService,
        ITaskSchedulerService taskSchedulerService,
        IUpdateCoordinator updateCoordinator,
        ILocalizationService? localizationService = null)
    {
        _settingsService = settingsService;
        _loggingService = loggingService;
        _taskSchedulerService = taskSchedulerService;
        _updateCoordinator = updateCoordinator;
        _localizationService = localizationService;

        var asmVer = typeof(SettingsViewModel).Assembly.GetName().Version;
        if (asmVer != null)
        {
            _currentVersion = $"{asmVer.Major}.{asmVer.Minor}.{asmVer.Build}";
        }

        _language = _settingsService.Language;
        _defaultExtractionFolder = _settingsService.DefaultExtractionFolder;
        _openFolderAfterExtraction = _settingsService.OpenExtractedFolderAfterExtraction;
        _keepTaskProgressWindowOpen = _settingsService.KeepTaskProgressWindowOpen;
        _deleteArchiveAfterExtraction = _settingsService.DeleteArchiveAfterExtraction;
        _doubleClickAction = _settingsService.DoubleClickAction;
        _confirmBeforeOverwriting = _settingsService.ConfirmBeforeOverwriting;
        _confirmBeforeDeleting = _settingsService.ConfirmBeforeDeleting;
        _defaultArchiveFormat = _settingsService.DefaultArchiveFormat;
        _defaultCompressionLevel = _settingsService.DefaultCompressionLevel;
        _themeMode = _settingsService.ThemeMode;
        _enableContextMenu = _settingsService.EnableContextMenu;
        _useCascadingContextMenu = _settingsService.UseCascadingContextMenu;
        _autoCloseTaskProgressWindow = _settingsService.AutoCloseTaskProgressWindow;
        _notifyOnTaskCompletion = _settingsService.NotifyOnTaskCompletion;
        _autoCheckUpdates = _settingsService.AutoCheckUpdates;
        _updateCheckFrequency = _settingsService.UpdateCheckFrequency;
        _lastCheckDisplay = _settingsService.LastUpdateCheckTime?.ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.CurrentCulture) ?? "Nunca";

        var enabledCommands = _settingsService.EnabledContextMenuCommands;
        _enableOpenWith = enabledCommands.Contains("OpenWith", StringComparer.OrdinalIgnoreCase);
        _enableExtractHere = enabledCommands.Contains("ExtractHere", StringComparer.OrdinalIgnoreCase);
        _enableExtractToFolder = enabledCommands.Contains("ExtractToFolder", StringComparer.OrdinalIgnoreCase);
        _enableExtractTo = enabledCommands.Contains("ExtractTo", StringComparer.OrdinalIgnoreCase);
        _enableCompressToZip = enabledCommands.Contains("CompressToZip", StringComparer.OrdinalIgnoreCase);
        _enableCompressTo7z = enabledCommands.Contains("CompressTo7z", StringComparer.OrdinalIgnoreCase);
    }

    public async Task SaveSettingsAsync()
    {
        _settingsService.Language = Language;
        if (_localizationService != null && !string.IsNullOrWhiteSpace(Language))
        {
            _localizationService.CurrentCulture = Language;
        }

        _settingsService.DefaultExtractionFolder = DefaultExtractionFolder;
        _settingsService.OpenExtractedFolderAfterExtraction = OpenFolderAfterExtraction;
        _settingsService.KeepTaskProgressWindowOpen = KeepTaskProgressWindowOpen;
        _settingsService.DeleteArchiveAfterExtraction = DeleteArchiveAfterExtraction;
        _settingsService.DoubleClickAction = DoubleClickAction;
        _settingsService.ConfirmBeforeOverwriting = ConfirmBeforeOverwriting;
        _settingsService.ConfirmBeforeDeleting = ConfirmBeforeDeleting;
        _settingsService.DefaultArchiveFormat = DefaultArchiveFormat;
        _settingsService.DefaultCompressionLevel = DefaultCompressionLevel;
        _settingsService.ThemeMode = ThemeMode;
        _settingsService.EnableContextMenu = EnableContextMenu;
        _settingsService.UseCascadingContextMenu = UseCascadingContextMenu;
        _settingsService.AutoCloseTaskProgressWindow = AutoCloseTaskProgressWindow;
        _settingsService.NotifyOnTaskCompletion = NotifyOnTaskCompletion;
        _settingsService.AutoCheckUpdates = AutoCheckUpdates;
        _settingsService.UpdateCheckFrequency = UpdateCheckFrequency;

        var enabledCommands = new List<string>();
        if (EnableOpenWith) enabledCommands.Add("OpenWith");
        if (EnableExtractHere) enabledCommands.Add("ExtractHere");
        if (EnableExtractToFolder) enabledCommands.Add("ExtractToFolder");
        if (EnableExtractTo) enabledCommands.Add("ExtractTo");
        if (EnableCompressToZip) enabledCommands.Add("CompressToZip");
        if (EnableCompressTo7z) enabledCommands.Add("CompressTo7z");
        _settingsService.EnabledContextMenuCommands = enabledCommands;

        await _settingsService.SaveAsync();

        // Sync with Windows Task Scheduler
        var updaterPath = _updateCoordinator.LocateUpdaterExecutable();
        if (AutoCheckUpdates && !string.IsNullOrEmpty(updaterPath))
        {
            await _taskSchedulerService.RegisterOrUpdateTaskAsync(updaterPath, UpdateCheckFrequency);
        }
        else
        {
            await _taskSchedulerService.UnregisterTaskAsync();
        }

        var exePath = Environment.ProcessPath ?? string.Empty;
        if (EnableContextMenu && !string.IsNullOrEmpty(exePath))
        {
            ContextMenuManager.RegisterContextMenu(
                exePath,
                enableOpenWith: EnableOpenWith,
                enableExtractHere: EnableExtractHere,
                enableExtractToFolder: EnableExtractToFolder,
                enableExtractTo: EnableExtractTo,
                enableCompressToZip: EnableCompressToZip,
                enableCompressTo7z: EnableCompressTo7z,
                useCascadingMenu: UseCascadingContextMenu);
        }
        else
        {
            ContextMenuManager.UnregisterContextMenu();
        }

        StatusMessage = "Settings saved successfully.";
    }

    public async Task CheckForUpdatesNowAsync()
    {
        IsCheckingForUpdates = true;
        UpdateStatusMessage = "Verificando atualizações...";
        try
        {
            var result = await _updateCoordinator.CheckForUpdatesAsync();
            if (result.IsUpdateAvailable)
            {
                IsUpdateAvailable = true;
                LatestVersionAvailable = result.LatestVersion;
                UpdateStatusMessage = $"Nova versão disponível: v{result.LatestVersion}";
            }
            else if (string.Equals(result.Status, "UpToDate", StringComparison.OrdinalIgnoreCase))
            {
                IsUpdateAvailable = false;
                UpdateStatusMessage = "Você está usando a versão mais recente.";
            }
            else if (string.Equals(result.Status, "SignatureInvalid", StringComparison.OrdinalIgnoreCase))
            {
                IsUpdateAvailable = false;
                UpdateStatusMessage = "Atualização recusada: assinatura digital inválida ou pacote adulterado.";
            }
            else if (string.Equals(result.Status, "DowngradeRejected", StringComparison.OrdinalIgnoreCase))
            {
                IsUpdateAvailable = false;
                UpdateStatusMessage = "Versão remota inferior à versão atual (downgrade recusado).";
            }
            else
            {
                IsUpdateAvailable = false;
                UpdateStatusMessage = !string.IsNullOrEmpty(result.ErrorMessage)
                    ? $"Não foi possível verificar atualizações: {result.ErrorMessage}"
                    : "Não foi possível verificar atualizações. O aplicativo funciona normalmente offline.";
            }

            LastCheckDisplay = DateTime.Now.ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.CurrentCulture);
        }
        catch (Exception ex)
        {
            UpdateStatusMessage = $"Erro ao verificar: {ex.Message}";
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    public async Task ApplyUpdateAsync()
    {
        UpdateStatusMessage = "Iniciando instalador da atualização...";
        await _updateCoordinator.LaunchUpdateInstallerAsync();
    }

    public void RegisterFileAssociations()
    {
        var exePath = Environment.ProcessPath ?? string.Empty;
        if (!string.IsNullOrEmpty(exePath))
        {
            FileAssociationManager.RegisterAssociations(exePath);
            StatusMessage = "Registered Firezip for all supported archive file types.";
        }
    }

    public void UnregisterFileAssociations()
    {
        FileAssociationManager.UnregisterAssociations();
        StatusMessage = "Unregistered Firezip file associations.";
    }

    public void OpenDefaultAppsSettings()
    {
        RegisterFileAssociations();
        FileAssociationManager.OpenDefaultAppsSettings();
        StatusMessage = "Abra as Configurações do Windows e selecione o Firezip como aplicativo padrão.";
    }

    public void OpenLogFolder()
    {
        var logDir = _loggingService.GetLogDirectory();
        ShellOperations.OpenFolderAndSelect(logDir);
    }
}
