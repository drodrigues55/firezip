using System.Globalization;
using Firezip.Core.Interfaces;
using Firezip.Core.Models;
using Firezip.Infrastructure.Logging;
using Firezip.Windows.Notifications;
using Firezip.Windows.Shell;
using Microsoft.Extensions.DependencyInjection;
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
    private long _sourceArchiveLength;

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

        // Initialize tickers from settings and constructor options
        OpenFolderCheckBox.IsChecked = openFolderAfter || _settings.OpenExtractedFolderAfterExtraction;
        KeepWindowOpenCheckBox.IsChecked = _settings.KeepTaskProgressWindowOpen;

        var fileName = Path.GetFileName(_primaryPath);
        if (_taskType == TaskType.Extract)
        {
            Title = $"Extraindo {fileName} — Firezip";
            TitleTextBlock.Text = $"Extraindo {fileName}";
            OperationIcon.Glyph = "\uE8B7"; // Folder/Open
            if (File.Exists(_primaryPath))
            {
                try { _sourceArchiveLength = new FileInfo(_primaryPath).Length; } catch { }
            }
        }
        else
        {
            Title = $"Compactando {fileName} — Firezip";
            TitleTextBlock.Text = $"Compactando para {fileName}";
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

        const int width = 560;
        const int height = 440;
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
                    FinishWithError($"Não foi possível abrir o arquivo: {ex.Message}");
                    return;
                }
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var progress = new Progress<OperationProgress>(p =>
            {
                // Item Progress Bar
                ItemProgressBar.Value = p.ItemPercentage;
                ItemPercentageTextBlock.Text = $"{p.ItemPercentage:0}%";
                CurrentItemTextBlock.Text = string.IsNullOrWhiteSpace(p.CurrentItemName) ? "Extraindo..." : p.CurrentItemName;

                // Total Progress Bar
                TaskProgressBar.Value = p.Percentage;
                TotalPercentageTextBlock.Text = $"{p.Percentage:0}%";
                ProcessedCountTextBlock.Text = $"{p.FilesProcessed} de {p.TotalFiles} itens ({ArchiveEntry.FormatBytes(p.BytesProcessed)} de {ArchiveEntry.FormatBytes(p.TotalBytes)})";

                // Speed and ETA
                SpeedTextBlock.Text = $"Velocidade: {p.FormattedSpeed}";
                EtaTextBlock.Text = $"Tempo restante: {p.FormattedRemainingTime}";

                // Compression Details Expander
                ElapsedTextBlock.Text = stopwatch.Elapsed.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
                if (p.TotalBytes > 0 && _sourceArchiveLength > 0)
                {
                    var ratio = Math.Clamp((double)_sourceArchiveLength / p.TotalBytes * 100.0, 0.0, 100.0);
                    var savings = Math.Max(0, 100.0 - ratio);
                    RatioTextBlock.Text = $"{ratio:F1}%";
                    SavingsTextBlock.Text = $"{savings:F1}% de espaço economizado";
                    SizesTextBlock.Text = $"Original: {ArchiveEntry.FormatBytes(p.TotalBytes)} | Compactado: {ArchiveEntry.FormatBytes(_sourceArchiveLength)}";
                }
                else
                {
                    SizesTextBlock.Text = $"Processado: {ArchiveEntry.FormatBytes(p.BytesProcessed)}";
                }
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
                    title: "Extração concluída",
                    message: $"Extraídos {result.FilesProcessed} arquivo(s) para {destinationDir}",
                    openFolderTarget: destinationDir);
            }
            else if (result.IsCancelled)
            {
                FinishWithCancellation();
            }
            else
            {
                FinishWithError(result.ErrorMessage ?? "A extração falhou.");
            }
        }
        catch (OperationCanceledException)
        {
            FinishWithCancellation();
        }
        catch (Exception ex)
        {
            FinishWithError($"Erro inesperado: {ex.Message}");
        }
    }

    private async Task RunCompressionAsync()
    {
        var outputArchive = _primaryPath;
        var sourcePaths = _sourcePaths ?? [];

        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var progress = new Progress<OperationProgress>(p =>
            {
                ItemProgressBar.Value = p.ItemPercentage;
                ItemPercentageTextBlock.Text = $"{p.ItemPercentage:0}%";
                CurrentItemTextBlock.Text = string.IsNullOrWhiteSpace(p.CurrentItemName) ? "Compactando..." : p.CurrentItemName;

                TaskProgressBar.Value = p.Percentage;
                TotalPercentageTextBlock.Text = $"{p.Percentage:0}%";
                ProcessedCountTextBlock.Text = $"{p.FilesProcessed} de {p.TotalFiles} itens ({ArchiveEntry.FormatBytes(p.BytesProcessed)} de {ArchiveEntry.FormatBytes(p.TotalBytes)})";

                SpeedTextBlock.Text = $"Velocidade: {p.FormattedSpeed}";
                EtaTextBlock.Text = $"Tempo restante: {p.FormattedRemainingTime}";

                ElapsedTextBlock.Text = stopwatch.Elapsed.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
                SizesTextBlock.Text = $"Processados: {ArchiveEntry.FormatBytes(p.BytesProcessed)}";
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
                    title: "Compactação concluída",
                    message: $"Arquivo {Path.GetFileName(outputArchive)} criado com sucesso.",
                    openFolderTarget: Path.GetDirectoryName(outputArchive));
            }
            else if (result.IsCancelled)
            {
                FinishWithCancellation();
            }
            else
            {
                FinishWithError(result.ErrorMessage ?? "A compactação falhou.");
            }
        }
        catch (OperationCanceledException)
        {
            FinishWithCancellation();
        }
        catch (Exception ex)
        {
            FinishWithError($"Erro inesperado: {ex.Message}");
        }
    }

    private async Task FinishWithSuccess(string title, string message, string? openFolderTarget)
    {
        _isCompleted = true;
        ItemProgressBar.Value = 100;
        TaskProgressBar.Value = 100;
        ItemPercentageTextBlock.Text = "100%";
        TotalPercentageTextBlock.Text = "100%";
        CurrentItemTextBlock.Text = "Concluído com sucesso";

        if (_settings.NotifyOnTaskCompletion)
        {
            _notificationService.TryShow(title, message);
        }

        // Automatic archive deletion if configured globally
        if (_taskType == TaskType.Extract && _settings.DeleteArchiveAfterExtraction && File.Exists(_primaryPath))
        {
            try
            {
                File.Delete(_primaryPath);
                _logger.Info($"Deleted source archive after successful extraction: {_primaryPath}");
            }
            catch (Exception ex)
            {
                _logger.Warn($"Could not auto-delete source archive: {ex.Message}");
            }
        }

        // Open folder if ticker checked
        var shouldOpenFolder = OpenFolderCheckBox.IsChecked == true;
        if (shouldOpenFolder && !string.IsNullOrEmpty(openFolderTarget))
        {
            ShellOperations.OpenFolderAndSelect(openFolderTarget);
        }

        var shouldKeepOpen = KeepWindowOpenCheckBox.IsChecked == true;
        if (shouldKeepOpen)
        {
            CancelCloseButton.Content = "Fechar";
            if (!string.IsNullOrEmpty(openFolderTarget))
            {
                OpenFolderButton.Visibility = Visibility.Visible;
            }

            // Exibir opção de excluir arquivo de origem se ainda existir
            if (_taskType == TaskType.Extract && File.Exists(_primaryPath))
            {
                DeleteSourceArchiveButton.Visibility = Visibility.Visible;
            }
        }
        else
        {
            await Task.Delay(400);
            Close();
        }
    }

    private void FinishWithError(string errorMessage)
    {
        _isCompleted = true;
        _logger.Error(errorMessage);
        TaskProgressBar.Visibility = Visibility.Collapsed;
        ItemProgressBar.Visibility = Visibility.Collapsed;
        CurrentItemTextBlock.Visibility = Visibility.Collapsed;
        SpeedTextBlock.Visibility = Visibility.Collapsed;
        EtaTextBlock.Visibility = Visibility.Collapsed;
        ProcessedCountTextBlock.Visibility = Visibility.Collapsed;

        StatusMessageTextBlock.Text = errorMessage;
        StatusMessageTextBlock.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
        StatusMessageTextBlock.Visibility = Visibility.Visible;

        CancelCloseButton.Content = "Fechar";
    }

    private void FinishWithCancellation()
    {
        _isCompleted = true;
        TaskProgressBar.Visibility = Visibility.Collapsed;
        ItemProgressBar.Visibility = Visibility.Collapsed;
        CurrentItemTextBlock.Text = "Operação cancelada";
        SpeedTextBlock.Visibility = Visibility.Collapsed;
        EtaTextBlock.Visibility = Visibility.Collapsed;
        ProcessedCountTextBlock.Visibility = Visibility.Collapsed;

        StatusMessageTextBlock.Text = "A operação foi cancelada pelo usuário.";
        StatusMessageTextBlock.Visibility = Visibility.Visible;

        CancelCloseButton.Content = "Fechar";
    }

    private void OnDeleteSourceArchiveClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (File.Exists(_primaryPath))
            {
                File.Delete(_primaryPath);
                DeleteSourceArchiveButton.IsEnabled = false;
                StatusMessageTextBlock.Text = "Arquivo de origem (.zip) excluído com sucesso.";
                StatusMessageTextBlock.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"];
                StatusMessageTextBlock.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            StatusMessageTextBlock.Text = $"Não foi possível excluir o arquivo: {ex.Message}";
            StatusMessageTextBlock.Visibility = Visibility.Visible;
        }
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
            CurrentItemTextBlock.Text = "Cancelando...";
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
