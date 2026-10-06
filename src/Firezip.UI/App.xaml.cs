using Firezip.Core.Interfaces;
using Firezip.Core.Models;
using Firezip.Formats;
using Firezip.Infrastructure.Logging;
using Firezip.Infrastructure.Settings;
using Firezip.Infrastructure.TempFiles;
using Firezip.UI.ViewModels;
using Firezip.UI.Views;
using Firezip.Windows.Notifications;
using Firezip.Infrastructure.Update;
using Firezip.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Firezip.UI;

public partial class App : Application
{
    private MainWindow? _window;
    private DispatcherQueue? _dispatcherQueue;

    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        InitializeComponent();

        var services = new ServiceCollection();

        // Register core services
        services.AddSingleton<IArchiveEngine, ArchiveEngine>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ILoggingService, LoggerService>();
        services.AddSingleton<TempFileManager>();
        services.AddSingleton<IWindowsNotificationService, WindowsNotificationService>();
        services.AddSingleton<ITaskSchedulerService, TaskSchedulerService>();
        services.AddSingleton<IUpdateCoordinator, ProcessUpdateCoordinator>();
        services.AddSingleton<ILocalizationService, LocalizationService>();

        // Register ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<SettingsViewModel>();

        Services = services.BuildServiceProvider();
    }

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        var notificationService = Services.GetRequiredService<IWindowsNotificationService>();
        notificationService.NotificationInvoked += OnNotificationInvoked;
        notificationService.Initialize();

        // Load settings before determining operational defaults
        try
        {
            var settings = Services.GetRequiredService<ISettingsService>();
            await settings.LoadAsync();

            var loc = Services.GetRequiredService<ILocalizationService>();
            if (!string.IsNullOrWhiteSpace(settings.Language))
            {
                loc.CurrentCulture = settings.Language;
            }
        }
        catch
        {
            // Keep the application available even if settings cannot be loaded.
        }

        // Process command-line arguments for dedicated task windows
        var cmdArgs = Environment.GetCommandLineArgs();
        if (cmdArgs.Length > 1)
        {
            var verb = cmdArgs[1];

            if (verb == "--extract-here" && cmdArgs.Length > 2)
            {
                var archive = cmdArgs[2];
                var targetDir = Path.GetDirectoryName(archive) ?? Environment.CurrentDirectory;
                var progressWin = new TaskProgressWindow(
                    TaskProgressWindow.TaskType.Extract,
                    archive,
                    targetDir,
                    ConflictPolicy.Overwrite,
                    openFolderAfter: false);
                progressWin.Activate();
                return;
            }

            if (verb == "--extract-to-folder" && cmdArgs.Length > 2)
            {
                var archive = cmdArgs[2];
                var folderName = Path.GetFileNameWithoutExtension(archive);
                var targetDir = Path.Combine(Path.GetDirectoryName(archive) ?? Environment.CurrentDirectory, folderName);
                var progressWin = new TaskProgressWindow(
                    TaskProgressWindow.TaskType.Extract,
                    archive,
                    targetDir,
                    ConflictPolicy.Overwrite,
                    openFolderAfter: true);
                progressWin.Activate();
                return;
            }

            if (verb == "--extract-to" && cmdArgs.Length > 2)
            {
                var archive = cmdArgs[2];
                var optionsWin = new TaskOptionsWindow(archive);
                optionsWin.Activate();
                return;
            }

            if (verb == "--compress-zip" && cmdArgs.Length > 2)
            {
                var target = cmdArgs[2];
                var zipPath = Path.HasExtension(target) && !Directory.Exists(target)
                    ? Path.ChangeExtension(target, ".zip")
                    : target.TrimEnd('\\', '/') + ".zip";

                var progressWin = new TaskProgressWindow(
                    TaskProgressWindow.TaskType.Compress,
                    zipPath,
                    destinationPath: zipPath,
                    compressFormat: ArchiveFormat.Zip,
                    sourcePaths: [target]);
                progressWin.Activate();
                return;
            }

            if (verb == "--compress-7z" && cmdArgs.Length > 2)
            {
                var target = cmdArgs[2];
                var sevenZipPath = Path.HasExtension(target) && !Directory.Exists(target)
                    ? Path.ChangeExtension(target, ".7z")
                    : target.TrimEnd('\\', '/') + ".7z";

                var progressWin = new TaskProgressWindow(
                    TaskProgressWindow.TaskType.Compress,
                    sevenZipPath,
                    destinationPath: sevenZipPath,
                    compressFormat: ArchiveFormat.SevenZip,
                    sourcePaths: [target]);
                progressWin.Activate();
                return;
            }
        }

        // Standard Full UI Launch (Start Menu, Desktop shortcut, or "Open with Firezip")
        _window = new MainWindow();
        _window.Activate();
        _window.ShowMainPage();
        _window.Closed += OnWindowClosed;

        if (cmdArgs.Length > 1 && File.Exists(cmdArgs[1]))
        {
            var mainVm = Services.GetRequiredService<MainViewModel>();
            await mainVm.OpenArchiveAsync(cmdArgs[1]);
        }
    }

    private void OnNotificationInvoked() =>
        _dispatcherQueue?.TryEnqueue(() => _window?.Activate());

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        var notificationService = Services.GetRequiredService<IWindowsNotificationService>();
        notificationService.NotificationInvoked -= OnNotificationInvoked;
        notificationService.Shutdown();
    }
}
