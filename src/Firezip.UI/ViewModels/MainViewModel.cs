using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Firezip.Core.Interfaces;
using Firezip.Core.Models;
using Firezip.Infrastructure.TempFiles;
using Firezip.Core.Collections;
using Firezip.Windows.Notifications;
using Firezip.Windows.Shell;

namespace Firezip.UI.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IArchiveEngine _engine;
    private readonly ISettingsService _settingsService;
    private readonly TempFileManager _tempFileManager;
    private readonly ILoggingService _loggingService;
    private readonly IWindowsNotificationService _notificationService;

    [ObservableProperty]
    private ArchiveInfo? _currentArchive;

    [ObservableProperty]
    private string _currentFolderPath = string.Empty;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _selectionStatusText = string.Empty;

    [ObservableProperty]
    private bool _hasArchive;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _busyTitle = string.Empty;

    [ObservableProperty]
    private string _busyDetails = string.Empty;

    [ObservableProperty]
    private double _operationPercent;

    [ObservableProperty]
    private string _operationSpeed = string.Empty;

    [ObservableProperty]
    private string _operationEta = string.Empty;

    private readonly Microsoft.UI.Dispatching.DispatcherQueue? _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
    private readonly Dictionary<string, List<ArchiveItemViewModel>> _folderHierarchyLookup = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ArchiveItemViewModel> _allViewModelsCache = [];
    private CancellationTokenSource? _searchDebounceCts;

    public ObservableRangeCollection<ArchiveItemViewModel> DisplayItems { get; } = [];

    private CancellationTokenSource? _activeCts;
    private CancellationTokenSource? _openCts;

    public Func<Task<string?>>? RequestPasswordCallback { get; set; }
    public Func<string, Task<ConflictResolution>>? ConflictCallback { get; set; }
    public Action<string, string>? ShowNotificationCallback { get; set; }
    public Func<string, string, string?, Task>? ShowErrorCallback { get; set; }
    public Func<string, Task<bool>>? ConfirmActionCallback { get; set; }

    public MainViewModel(
        IArchiveEngine engine,
        ISettingsService settingsService,
        TempFileManager tempFileManager,
        ILoggingService loggingService,
        IWindowsNotificationService notificationService)
    {
        _engine = engine;
        _settingsService = settingsService;
        _tempFileManager = tempFileManager;
        _loggingService = loggingService;
        _notificationService = notificationService;
    }

    partial void OnCurrentArchiveChanged(ArchiveInfo? oldValue, ArchiveInfo? newValue)
    {
        if (newValue == null)
        {
            _folderHierarchyLookup.Clear();
            _allViewModelsCache.Clear();
        }
        else
        {
            BuildHierarchyIndex(newValue);
        }
    }

    partial void OnSearchQueryChanged(string value)
    {
        _searchDebounceCts?.Cancel();
        _searchDebounceCts?.Dispose();
        _searchDebounceCts = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            RefreshDisplayItems();
            return;
        }

        var cts = new CancellationTokenSource();
        _searchDebounceCts = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(150, cts.Token);
                if (!cts.IsCancellationRequested)
                {
                    RunOnUIThread(() =>
                    {
                        if (!cts.IsCancellationRequested)
                        {
                            RefreshDisplayItems();
                        }
                    });
                }
            }
            catch (OperationCanceledException) { }
        });
    }

    private void RunOnUIThread(Action action)
    {
        if (_dispatcherQueue != null && !_dispatcherQueue.HasThreadAccess)
        {
            _dispatcherQueue.TryEnqueue(() => action());
        }
        else
        {
            action();
        }
    }

    public async Task OpenArchiveAsync(string filePath, string? password = null)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return;

        using var openCts = new CancellationTokenSource();
        _openCts = openCts;
        IsBusy = true;
        BusyTitle = "Opening Archive";
        BusyDetails = Path.GetFileName(filePath);

        try
        {
            var passwordAttempt = password;
            while (true)
            {
                try
                {
                    var info = await _engine.OpenArchiveAsync(filePath, passwordAttempt, openCts.Token);
                    CurrentArchive = info;
                    HasArchive = true;
                    CurrentFolderPath = string.Empty;
                    SearchQuery = string.Empty;
                    RefreshDisplayItems();
                    UpdateStatusText();
                    _loggingService.Info($"Opened archive '{Path.GetFileName(filePath)}' as {info.Format} with {info.Entries.Count} entries.");
                    return;
                }
                catch (OperationCanceledException) when (openCts.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // If encrypted and no password provided, prompt user and retry.
                    if (RequestPasswordCallback != null && ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase))
                    {
                        passwordAttempt = await RequestPasswordCallback();
                        if (string.IsNullOrEmpty(passwordAttempt))
                            return;

                        continue;
                    }

                    var rootCause = ex.GetBaseException();
                    _loggingService.Error($"Failed to open archive '{Path.GetFileName(filePath)}' ({rootCause.GetType().Name}).");

                    var title = $"Could not open {Path.GetFileName(filePath)}";
                    var errorDetails = $"{rootCause.GetType().Name}: {rootCause.Message}";
                    StatusText = $"Could not open {Path.GetFileName(filePath)}.";
                    if (ShowErrorCallback != null)
                        await ShowErrorCallback(title, errorDetails, ex.ToString());
                    return;
                }
            }
        }
        finally
        {
            if (ReferenceEquals(_openCts, openCts))
                _openCts = null;
            if (_activeCts == null)
                IsBusy = false;
        }
    }

    public void NavigateToFolder(string folderPath)
    {
        CurrentFolderPath = folderPath.Replace('\\', '/').Trim('/');
        RefreshDisplayItems();
    }

    public void CloseArchive()
    {
        if (IsBusy)
            return;

        CurrentArchive = null;
        HasArchive = false;
        CurrentFolderPath = string.Empty;
        SearchQuery = string.Empty;
        _folderHierarchyLookup.Clear();
        _allViewModelsCache.Clear();
        DisplayItems.Clear();
        SelectionStatusText = string.Empty;
        StatusText = "Ready";
    }

    public void NavigateUp()
    {
        if (string.IsNullOrEmpty(CurrentFolderPath))
            return;

        var lastSlash = CurrentFolderPath.LastIndexOf('/');
        CurrentFolderPath = lastSlash >= 0 ? CurrentFolderPath[..lastSlash] : string.Empty;
        RefreshDisplayItems();
    }

    private void BuildHierarchyIndex(ArchiveInfo archive)
    {
        _folderHierarchyLookup.Clear();
        _allViewModelsCache.Clear();

        var knownDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            if (entry.IsDirectory)
            {
                var norm = entry.FullPath.Replace('\\', '/').Trim('/');
                if (!string.IsNullOrEmpty(norm))
                {
                    knownDirs.Add(norm);
                }
            }
        }

        var seenInFolder = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in archive.Entries)
        {
            var vm = ArchiveItemViewModel.FromEntry(entry);
            _allViewModelsCache.Add(vm);

            var parent = entry.ParentDirectory.Replace('\\', '/').Trim('/');
            var uniqueKey = $"{parent}|{vm.Name}";
            if (seenInFolder.Add(uniqueKey))
            {
                if (!_folderHierarchyLookup.TryGetValue(parent, out var list))
                {
                    list = [];
                    _folderHierarchyLookup[parent] = list;
                }
                list.Add(vm);
            }

            // Traverse ancestors to synthesize missing intermediate directories
            var currentParent = parent;
            while (!string.IsNullOrEmpty(currentParent))
            {
                if (knownDirs.Add(currentParent))
                {
                    var lastSlash = currentParent.LastIndexOf('/');
                    var dirName = lastSlash >= 0 ? currentParent[(lastSlash + 1)..] : currentParent;
                    var ancestorParent = lastSlash >= 0 ? currentParent[..lastSlash] : string.Empty;

                    var syntheticDir = new ArchiveItemViewModel
                    {
                        Name = dirName,
                        FullPath = currentParent,
                        IsDirectory = true
                    };

                    _allViewModelsCache.Add(syntheticDir);

                    var ancestorKey = $"{ancestorParent}|{dirName}";
                    if (seenInFolder.Add(ancestorKey))
                    {
                        if (!_folderHierarchyLookup.TryGetValue(ancestorParent, out var ancestorList))
                        {
                            ancestorList = [];
                            _folderHierarchyLookup[ancestorParent] = ancestorList;
                        }
                        ancestorList.Add(syntheticDir);
                    }
                }
                else
                {
                    break;
                }

                var nextSlash = currentParent.LastIndexOf('/');
                currentParent = nextSlash >= 0 ? currentParent[..nextSlash] : string.Empty;
            }
        }

        // Pre-sort each directory's items: directories first, then alphabetical by name
        foreach (var list in _folderHierarchyLookup.Values)
        {
            list.Sort((a, b) =>
            {
                int dirCompare = b.IsDirectory.CompareTo(a.IsDirectory);
                if (dirCompare != 0) return dirCompare;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
        }
    }

    public void RefreshDisplayItems()
    {
        if (CurrentArchive == null)
        {
            DisplayItems.Clear();
            return;
        }

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var query = SearchQuery.Trim();
            var matches = _allViewModelsCache
                .Where(vm => vm.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                             vm.FullPath.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(vm => vm.IsDirectory)
                .ThenBy(vm => vm.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            DisplayItems.ReplaceRange(matches);
            return;
        }

        var currentNorm = CurrentFolderPath.Replace('\\', '/').Trim('/');
        var itemsToShow = new List<ArchiveItemViewModel>();

        // If in a subfolder and not searching, prepend ".." parent folder link
        if (!string.IsNullOrEmpty(currentNorm))
        {
            itemsToShow.Add(ArchiveItemViewModel.CreateParentFolderLink(CurrentFolderPath));
        }

        if (_folderHierarchyLookup.TryGetValue(currentNorm, out var directChildren))
        {
            itemsToShow.AddRange(directChildren);
        }

        DisplayItems.ReplaceRange(itemsToShow);
    }

    public async Task ExtractAllAsync(string destinationDirectory, ConflictPolicy policy = ConflictPolicy.AskUser, bool openFolderAfter = true)
    {
        if (CurrentArchive == null) return;

        await ExecuteExtractionAsync(new ExtractionRequest
        {
            ArchiveFilePath = CurrentArchive.FilePath,
            DestinationDirectory = destinationDirectory,
            DefaultConflictPolicy = policy,
            ConflictCallback = ConflictCallback
        }, openFolderAfter);
    }

    public async Task ExtractSelectedAsync(IEnumerable<ArchiveItemViewModel> selectedItems, string destinationDirectory, ConflictPolicy policy = ConflictPolicy.AskUser)
    {
        if (CurrentArchive == null) return;

        var selectedPaths = new HashSet<string>(
            selectedItems.Where(i => !i.IsParentFolderLink).Select(i => i.FullPath),
            StringComparer.OrdinalIgnoreCase);

        var entriesToExtract = CurrentArchive.Entries.Where(e =>
            selectedPaths.Contains(e.FullPath) ||
            selectedPaths.Any(p => e.FullPath.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase))).ToList();

        await ExecuteExtractionAsync(new ExtractionRequest
        {
            ArchiveFilePath = CurrentArchive.FilePath,
            DestinationDirectory = destinationDirectory,
            EntriesToExtract = entriesToExtract,
            DefaultConflictPolicy = policy,
            ConflictCallback = ConflictCallback
        }, _settingsService.OpenExtractedFolderAfterExtraction);
    }

    /// <summary>
    /// Extracts selected archive entries into a private session directory so Windows Explorer
    /// can consume them as dragged storage items. The session TempFileManager owns cleanup.
    /// </summary>
    public async Task<IReadOnlyList<string>> MaterializeSelectedItemsForDragAsync(
        IReadOnlyList<ArchiveItemViewModel> selectedItems,
        CancellationToken cancellationToken = default)
    {
        if (CurrentArchive == null || selectedItems.Count == 0)
            return [];

        var archivePath = CurrentArchive.FilePath;
        var selectedPaths = new HashSet<string>(
            selectedItems.Where(i => !i.IsParentFolderLink).Select(i => i.FullPath),
            StringComparer.OrdinalIgnoreCase);
        if (selectedPaths.Count == 0)
            return [];

        var entriesToExtract = CurrentArchive.Entries.Where(e =>
            selectedPaths.Contains(e.FullPath) ||
            selectedPaths.Any(p => e.FullPath.StartsWith(p.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase))).ToList();
        if (entriesToExtract.Count == 0)
            return [];

        var tempDir = _tempFileManager.CreateTempDirectory("drag-out");
        var result = await _engine.ExtractAsync(new ExtractionRequest
        {
            ArchiveFilePath = archivePath,
            DestinationDirectory = tempDir,
            EntriesToExtract = entriesToExtract,
            DefaultConflictPolicy = ConflictPolicy.Overwrite
        }, cancellationToken);

        if (!result.Success)
        {
            _tempFileManager.CleanupDirectory(tempDir);
            if (!result.IsCancelled)
                await ShowErrorAsync("Could not prepare dragged items", result);
            return [];
        }

        var materialized = new List<string>();
        foreach (var item in selectedItems.Where(i => !i.IsParentFolderLink))
        {
            var relativePath = item.FullPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var candidate = Path.GetFullPath(Path.Combine(tempDir, relativePath));
            var rootPrefix = Path.GetFullPath(tempDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                continue;
            if (File.Exists(candidate) || Directory.Exists(candidate))
                materialized.Add(candidate);
        }

        return materialized;
    }

    public async Task OpenFileItemAsync(ArchiveItemViewModel item)
    {
        if (CurrentArchive == null || item.IsDirectory)
            return;

        var tempDir = _tempFileManager.CreateTempDirectory("preview");
        var tempFile = Path.Combine(tempDir, item.Name);

        var matchingEntry = CurrentArchive.GetEntryByPath(item.FullPath) ?? CurrentArchive.Entries.FirstOrDefault(e => e.FullPath == item.FullPath);
        if (matchingEntry == null) return;

        var result = await _engine.ExtractAsync(new ExtractionRequest
        {
            ArchiveFilePath = CurrentArchive.FilePath,
            DestinationDirectory = tempDir,
            EntriesToExtract = [matchingEntry],
            DefaultConflictPolicy = ConflictPolicy.Overwrite
        });

        if (result.Success)
        {
            var extractedFiles = Directory.GetFiles(tempDir, "*.*", SearchOption.AllDirectories);
            if (extractedFiles.Length > 0)
            {
                ShellOperations.OpenFileWithDefaultProgram(extractedFiles[0]);
            }
        }
    }

    public async Task<OperationResult> CreateArchiveAsync(
        string destinationFilePath,
        ArchiveFormat format,
        IReadOnlyList<string> sourcePaths,
        CompressionOptions options)
    {
        _activeCts = new CancellationTokenSource();
        IsBusy = true;
        BusyTitle = "Creating Archive";
        BusyDetails = Path.GetFileName(destinationFilePath);
        OperationPercent = 0;

        var progress = new Progress<OperationProgress>(p =>
        {
            OperationPercent = p.Percentage;
            OperationSpeed = p.FormattedSpeed;
            OperationEta = p.FormattedRemainingTime;
            BusyDetails = $"{p.CurrentItemName} ({p.FilesProcessed}/{p.TotalFiles})";
        });

        try
        {
            var result = await _engine.CreateArchiveAsync(new CompressionRequest
            {
                OutputArchiveFilePath = destinationFilePath,
                Format = format,
                SourcePaths = sourcePaths,
                Options = options,
                Progress = progress
            }, _activeCts.Token);

            if (result.Success)
            {
                ShowNotificationCallback?.Invoke("Compression Complete", $"Successfully created {Path.GetFileName(destinationFilePath)}");
                _notificationService.TryShow("Archive created", Path.GetFileName(destinationFilePath));
                await OpenArchiveAsync(destinationFilePath);
            }
            else if (!result.IsCancelled)
            {
                await ShowErrorAsync("Compression failed", result);
            }

            return result;
        }
        finally
        {
            IsBusy = false;
            _activeCts?.Dispose();
            _activeCts = null;
        }
    }

    public async Task TestArchiveAsync()
    {
        if (CurrentArchive == null) return;

        _activeCts = new CancellationTokenSource();
        IsBusy = true;
        BusyTitle = "Testing Archive Integrity";
        BusyDetails = Path.GetFileName(CurrentArchive.FilePath);
        OperationPercent = 0;

        var progress = new Progress<OperationProgress>(p =>
        {
            OperationPercent = p.Percentage;
            BusyDetails = $"Verifying: {p.CurrentItemName}";
        });

        try
        {
            var result = await _engine.TestArchiveAsync(new TestArchiveRequest
            {
                ArchiveFilePath = CurrentArchive.FilePath,
                Progress = progress
            }, _activeCts.Token);

            if (result.Success)
            {
                ShowNotificationCallback?.Invoke("Integrity Test Passed", $"No errors were found in {Path.GetFileName(CurrentArchive.FilePath)} ({result.FilesProcessed} files verified).");
                _notificationService.TryShow("Archive check passed", $"{result.FilesProcessed} file(s) verified.");
            }
            else
            {
                await ShowErrorAsync("Integrity test failed", result);
            }
        }
        finally
        {
            IsBusy = false;
            _activeCts?.Dispose();
            _activeCts = null;
        }
    }

    public async Task DeleteSelectedEntriesAsync(IEnumerable<ArchiveItemViewModel>? items = null)
    {
        if (CurrentArchive == null) return;

        var targetItems = (items ?? DisplayItems.Where(i => !i.IsParentFolderLink))
            .Where(i => !i.IsParentFolderLink)
            .ToList();

        if (targetItems.Count == 0) return;

        if (_settingsService.ConfirmBeforeDeleting && ConfirmActionCallback != null)
        {
            var prompt = targetItems.Count == 1
                ? $"Are you sure you want to permanently delete \"{targetItems[0].Name}\" from the archive?"
                : $"Are you sure you want to permanently delete {targetItems.Count} items from the archive?";

            var confirmed = await ConfirmActionCallback(prompt);
            if (!confirmed) return;
        }

        _activeCts = new CancellationTokenSource();
        IsBusy = true;
        BusyTitle = "Deleting Entries";
        BusyDetails = $"{targetItems.Count} item(s)...";
        OperationPercent = 0;

        try
        {
            var archivePath = CurrentArchive.FilePath;
            var currentFolder = CurrentFolderPath;
            var pathsToDelete = targetItems.Select(i => i.FullPath).ToList();

            var result = await _engine.DeleteEntriesAsync(archivePath, pathsToDelete, _activeCts.Token);

            if (result.Success)
            {
                ShowNotificationCallback?.Invoke("Items Deleted", $"Successfully deleted {result.FilesProcessed} entry(ies).");
                _notificationService.TryShow("Items deleted", $"{result.FilesProcessed} entry(ies) deleted from archive.");

                if (File.Exists(archivePath))
                {
                    await OpenArchiveAsync(archivePath);
                    if (!string.IsNullOrEmpty(currentFolder))
                    {
                        NavigateToFolder(currentFolder);
                    }
                }
                else
                {
                    CloseArchive();
                }
            }
            else if (!result.IsCancelled)
            {
                await ShowErrorAsync("Delete failed", result);
            }
        }
        finally
        {
            IsBusy = false;
            _activeCts?.Dispose();
            _activeCts = null;
        }
    }

    public async Task AddFilesToArchiveAsync(IReadOnlyList<string> sourcePaths)
    {
        if (CurrentArchive == null || sourcePaths == null || sourcePaths.Count == 0)
            return;

        _activeCts = new CancellationTokenSource();
        IsBusy = true;
        BusyTitle = "Adding Files";
        BusyDetails = $"{sourcePaths.Count} item(s)...";
        OperationPercent = 0;

        try
        {
            var archivePath = CurrentArchive.FilePath;
            var currentFolder = CurrentFolderPath;

            var result = await _engine.AddEntriesAsync(
                archivePath,
                sourcePaths,
                string.IsNullOrWhiteSpace(currentFolder) ? null : currentFolder,
                _activeCts.Token);

            if (result.Success)
            {
                ShowNotificationCallback?.Invoke("Files Added", $"Successfully added {result.FilesProcessed} item(s) to archive.");
                _notificationService.TryShow("Files added", $"{result.FilesProcessed} item(s) added to archive.");

                await OpenArchiveAsync(archivePath);
                if (!string.IsNullOrEmpty(currentFolder))
                {
                    NavigateToFolder(currentFolder);
                }
            }
            else if (!result.IsCancelled)
            {
                await ShowErrorAsync("Add files failed", result);
            }
        }
        finally
        {
            IsBusy = false;
            _activeCts?.Dispose();
            _activeCts = null;
        }
    }

    public void CancelOperation()
    {
        _activeCts?.Cancel();
        _openCts?.Cancel();
    }

    public void UpdateSelection(IEnumerable<ArchiveItemViewModel> selectedItems)
    {
        var count = selectedItems.Count(i => !i.IsParentFolderLink);
        if (count == 0)
        {
            SelectionStatusText = string.Empty;
        }
        else
        {
            var totalBytes = selectedItems.Where(i => !i.IsParentFolderLink && !i.IsDirectory).Sum(i => i.Size);
            SelectionStatusText = $"{count} item(s) selected ({ArchiveEntry.FormatBytes(totalBytes)})";
        }
    }

    private async Task ExecuteExtractionAsync(ExtractionRequest request, bool openFolderAfter)
    {
        _activeCts = new CancellationTokenSource();
        IsBusy = true;
        BusyTitle = "Extracting Archive";
        BusyDetails = Path.GetFileName(request.ArchiveFilePath);
        OperationPercent = 0;

        var progress = new Progress<OperationProgress>(p =>
        {
            OperationPercent = p.Percentage;
            OperationSpeed = p.FormattedSpeed;
            OperationEta = p.FormattedRemainingTime;
            BusyDetails = $"{p.CurrentItemName} ({p.FilesProcessed}/{p.TotalFiles})";
        });

        var requestWithProgress = request with { Progress = progress };

        try
        {
            var result = await _engine.ExtractAsync(requestWithProgress, _activeCts.Token);

            if (result.Success)
            {
                ShowNotificationCallback?.Invoke("Extraction Complete", $"Extracted {result.FilesProcessed} file(s) to {request.DestinationDirectory}");
                _notificationService.TryShow("Extraction complete", $"{result.FilesProcessed} file(s) extracted.");
                if (openFolderAfter)
                {
                    ShellOperations.OpenFolderAndSelect(request.DestinationDirectory);
                }
            }
            else if (!result.IsCancelled)
            {
                await ShowErrorAsync("Extraction failed", result);
            }
        }
        finally
        {
            IsBusy = false;
            _activeCts?.Dispose();
            _activeCts = null;
        }
    }

    private void UpdateStatusText()
    {
        if (CurrentArchive == null)
        {
            StatusText = "Ready";
            return;
        }

        var ratioStr = CurrentArchive.OverallCompressionRatio.HasValue
            ? $", {CurrentArchive.OverallCompressionRatio.Value:F1}%"
            : string.Empty;

        StatusText = $"{CurrentArchive.TotalFiles} file(s), {CurrentArchive.TotalDirectories} folder(s) | " +
                     $"{ArchiveEntry.FormatBytes(CurrentArchive.TotalUncompressedSize)} " +
                     $"(Compressed: {ArchiveEntry.FormatBytes(CurrentArchive.TotalCompressedSize)}{ratioStr})";
    }

    private async Task ShowErrorAsync(string title, OperationResult result)
    {
        var message = result.ErrorMessage ?? "The operation could not be completed.";
        var details = string.IsNullOrWhiteSpace(result.DetailedError)
            ? message
            : result.DetailedError;

        _loggingService.Error($"{title}. Files processed before failure: {result.FilesProcessed}.");

        if (ShowErrorCallback != null)
            await ShowErrorCallback(title, message, details);
        else
            ShowNotificationCallback?.Invoke(title, "The operation could not be completed. Open the error details for more information.");
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _activeCts?.Dispose();
            _activeCts = null;
        }
    }
}
