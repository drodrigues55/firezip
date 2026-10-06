using Firezip.Core.Interfaces;
using Firezip.Core.Models;
using Firezip.Infrastructure.Logging;
using Firezip.Infrastructure.Settings;
using Firezip.Windows.Notifications;
using Firezip.Windows.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace Firezip.UI.Views;

public sealed partial class TaskProgressWindow : Window, IDisposable
{
    public enum TaskType { Extract, Compress }

    private readonly TaskType _taskType;
    private readonly string _primaryPath;
    private readonly string _destinationPath;
    private readonly ConflictPolicy _conflictPolicy;
    private readonly bool _openFolderAfter;
    private readonly ArchiveFormat _compressFormat;
    private readonly IReadOnlyList<string>? _sourcePaths;
    private readonly CompressionOptions? _compressionOptions;

    private readonly IArchiveEngine _engine;
    private readonly ISettingsService _settings;
    private readonly ILoggingService _logger;
    private readonly IWindowsNotificationService _notificationService;

    private readonly CancellationTokenSource _cts = new();
    private bool _isCompleted;
    private bool _hasStarted;

    public TaskProgressWindow(
        TaskType taskType,
        string primaryPath,
        string destinationPath,
        ConflictPolicy conflictPolicy = ConflictPolicy.Overwrite,
        bool openFolderAfter = false,
        ArchiveFormat compressFormat = ArchiveFormat.Zip,
        IReadOnlyList<string>? sourcePaths = null,
        CompressionOptions? compressionOptions = null)
    {
        InitializeComponent();

        _taskType = taskType;
        _primaryPath = primaryPath;
        _destinationPath = destinationPath;
        _conflictPolicy = conflictPolicy;
        _openFolderAfter = openFolderAfter;
        _compressFormat = compressFormat;
        _sourcePaths = sourcePaths;
        _compressionOptions = compressionOptions ?? CompressionOptions.Default;

        _engine = App.Services.GetRequiredService<IArchiveEngine>();
        _settings = App.Services.GetRequiredService<ISettingsService>();
        _logger = App.Services.GetRequiredService<ILoggingService>();
        _notificationService = App.Services.GetRequiredService<IWindowsNotificationService>();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        ConfigureWindowSizeAndPosition();

        var fileName = Path.GetFileName(_primaryPath);
        if (_taskType == TaskType.Extract)
        {
            Title = $"Extracting {fileName} — Firezip";
            TitleTextBlock.Text = $"Extracting {fileName}";
            OperationIcon.Glyph = "\uE8B7"; // Folder/Open
        }
        else
        {
            Title = $"Compressing {fileName} — Firezip";
            TitleTextBlock.Text = $"Compressing to {fileName}";
            OperationIcon.Glyph = "\uE8B9"; // Save/Archive
        }

        Closed += (_, _) =>
        {
            if (!_isCompleted)
                _cts.Cancel();
            _cts.Dispose();
            Application.Current.Exit();
        };

        Activated += OnWindowActivated;
    }

    private void ConfigureWindowSizeAndPosition()
    {
        try
        {
            AppWindow.SetIcon("Assets/AppIcon.ico");
        }
        catch { }

        const int width = 500;
        const int height = 240;
        AppWindow.Resize(new SizeInt32(width, height));

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsResizable = false;
        }

        try
        {
            var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            if (displayArea != null)
            {
                var x = displayArea.WorkArea.X + (displayArea.WorkArea.Width - width) / 2;
                var y = displayArea.WorkArea.Y + (displayArea.WorkArea.Height - height) / 2;
                AppWindow.Move(new PointInt32(x, y));
            }
        }
        catch { }
    }

    private async void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (_hasStarted) return;
        _hasStarted = true;

        await Task.Delay(100); // Allow XAML tree to settle
        if (_taskType == TaskType.Extract)
        {
            await RunExtractionAsync();
        }
        else
        {
            await RunCompressionAsync();
        }
    }

    private async Task RunExtractionAsync()
    {
        var archivePath = _primaryPath;
        var destinationDir = _destinationPath;

        try
        {
            // First open archive to check for password if needed
            string? password = null;
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    await _engine.OpenArchiveAsync(archivePath, password, _cts.Token);
                    break;
                }
                catch (Exception ex) when (ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase))
                {
                    password = await PromptPasswordAsync();
                    if (string.IsNullOrEmpty(password))
                    {
                        FinishWithCancellation();
                        return;
                    }
                }
                catch (OperationCanceledException)
                {
                    FinishWithCancellation();
                    return;
                }
                catch (Exception ex)
                {
                    FinishWithError($"Could not open archive: {ex.Message}");
                    return;
                }
            }

            var progress = new Progress<OperationProgress>(p =>
            {
                TaskProgressBar.Value = p.Percentage;
                CurrentItemTextBlock.Text = string.IsNullOrWhiteSpace(p.CurrentItemName) ? "Extracting..." : p.CurrentItemName;
                ProcessedCountTextBlock.Text = $"{p.FilesProcessed} of {p.TotalFiles} file(s)";
                SpeedAndEtaTextBlock.Text = $"{p.FormattedSpeed} • ETA: {p.FormattedRemainingTime}";
            });

            var request = new ExtractionRequest
            {
                ArchiveFilePath = archivePath,
                DestinationDirectory = destinationDir,
                DefaultConflictPolicy = _conflictPolicy,
                ConflictCallback = PromptConflictAsync,
                Password = password,
                Progress = progress
            };

            var result = await _engine.ExtractAsync(request, _cts.Token);
            if (result.Success)
            {
                await FinishWithSuccess(
                    title: "Extraction complete",
                    message: $"Extracted {result.FilesProcessed} file(s) to {destinationDir}",
                    openFolderTarget: destinationDir);
            }
            else if (result.IsCancelled)
            {
                FinishWithCancellation();
            }
            else
            {
                FinishWithError(result.ErrorMessage ?? "Extraction failed.");
            }
        }
        catch (OperationCanceledException)
        {
            FinishWithCancellation();
        }
        catch (Exception ex)
        {
            FinishWithError($"Unexpected error: {ex.Message}");
        }
    }

    private async Task RunCompressionAsync()
    {
        var outputArchive = _primaryPath;
        var sourcePaths = _sourcePaths ?? [];

        try
        {
            var progress = new Progress<OperationProgress>(p =>
            {
                TaskProgressBar.Value = p.Percentage;
                CurrentItemTextBlock.Text = string.IsNullOrWhiteSpace(p.CurrentItemName) ? "Compressing..." : p.CurrentItemName;
                ProcessedCountTextBlock.Text = $"{p.FilesProcessed} of {p.TotalFiles} file(s)";
                SpeedAndEtaTextBlock.Text = $"{p.FormattedSpeed} • ETA: {p.FormattedRemainingTime}";
            });

            var request = new CompressionRequest
            {
                OutputArchiveFilePath = outputArchive,
                Format = _compressFormat,
                SourcePaths = sourcePaths,
                Options = _compressionOptions ?? CompressionOptions.Default,
                Progress = progress
            };

            var result = await _engine.CreateArchiveAsync(request, _cts.Token);
            if (result.Success)
            {
                await FinishWithSuccess(
                    title: "Compression complete",
                    message: $"Created {Path.GetFileName(outputArchive)}",
                    openFolderTarget: Path.GetDirectoryName(outputArchive));
            }
            else if (result.IsCancelled)
            {
                FinishWithCancellation();
            }
            else
            {
                FinishWithError(result.ErrorMessage ?? "Compression failed.");
            }
        }
        catch (OperationCanceledException)
        {
            FinishWithCancellation();
        }
        catch (Exception ex)
        {
            FinishWithError($"Unexpected error: {ex.Message}");
        }
    }

    private async Task FinishWithSuccess(string title, string message, string? openFolderTarget)
    {
        _isCompleted = true;
        TaskProgressBar.Value = 100;
        CurrentItemTextBlock.Text = "Complete";

        if (_settings.NotifyOnTaskCompletion)
        {
            _notificationService.TryShow(title, message);
        }

        if (_openFolderAfter && !string.IsNullOrEmpty(openFolderTarget))
        {
            ShellOperations.OpenFolderAndSelect(openFolderTarget);
        }

        if (_settings.AutoCloseTaskProgressWindow)
        {
            await Task.Delay(350);
            Close();
        }
        else
        {
            CancelCloseButton.Content = "Close";
            if (!string.IsNullOrEmpty(openFolderTarget))
                OpenFolderButton.Visibility = Visibility.Visible;
        }
    }

    private void FinishWithError(string errorMessage)
    {
        _isCompleted = true;
        _logger.Error(errorMessage);
        TaskProgressBar.Visibility = Visibility.Collapsed;
        CurrentItemTextBlock.Visibility = Visibility.Collapsed;
        SpeedAndEtaTextBlock.Visibility = Visibility.Collapsed;
        ProcessedCountTextBlock.Visibility = Visibility.Collapsed;

        StatusMessageTextBlock.Text = errorMessage;
        StatusMessageTextBlock.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
        StatusMessageTextBlock.Visibility = Visibility.Visible;

        CancelCloseButton.Content = "Close";
    }

    private void FinishWithCancellation()
    {
        _isCompleted = true;
        TaskProgressBar.Visibility = Visibility.Collapsed;
        CurrentItemTextBlock.Text = "Cancelled";
        SpeedAndEtaTextBlock.Visibility = Visibility.Collapsed;
        ProcessedCountTextBlock.Visibility = Visibility.Collapsed;

        StatusMessageTextBlock.Text = "Operation was cancelled.";
        StatusMessageTextBlock.Visibility = Visibility.Visible;

        CancelCloseButton.Content = "Close";
    }

    private async Task<string?> PromptPasswordAsync()
    {
        var dialog = new PasswordDialog { XamlRoot = Content.XamlRoot };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? dialog.Password : null;
    }

    private async Task<ConflictResolution> PromptConflictAsync(string existingFilePath)
    {
        var dialog = new ConflictDialog(existingFilePath) { XamlRoot = Content.XamlRoot };
        await dialog.ShowAsync();
        return dialog.Resolution;
    }

    private void OnCancelCloseClick(object sender, RoutedEventArgs e)
    {
        if (_isCompleted)
        {
            Close();
        }
        else
        {
            _cts.Cancel();
            CancelCloseButton.IsEnabled = false;
            CurrentItemTextBlock.Text = "Cancelling...";
        }
    }

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        var target = _taskType == TaskType.Extract ? _destinationPath : Path.GetDirectoryName(_primaryPath);
        if (!string.IsNullOrEmpty(target))
        {
            ShellOperations.OpenFolderAndSelect(target);
        }
        Close();
    }

    public void Dispose()
    {
        _cts.Dispose();
    }
}
