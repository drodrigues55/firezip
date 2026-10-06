using Firezip.Core.Models;
using Firezip.UI.ViewModels;
using Firezip.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

namespace Firezip.UI;

public sealed partial class MainPage : Page
{
    private static readonly string[] SupportedArchiveExtensions =
    [
        ".zip", ".zipx", ".jar", ".apk", ".7z", ".rar",
        ".tar", ".gz", ".tgz", ".bz2", ".tbz2", ".xz", ".txz"
    ];

    public MainViewModel ViewModel { get; }

    public MainPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<MainViewModel>();

        // Wire callbacks to WinUI UI thread dialogs
        ViewModel.RequestPasswordCallback = PromptPasswordAsync;
        ViewModel.ConflictCallback = PromptConflictAsync;
        ViewModel.ShowNotificationCallback = ShowNotification;
        ViewModel.ShowErrorCallback = ShowErrorAsync;
        ViewModel.ConfirmActionCallback = PromptDeleteConfirmationAsync;
        RegisterKeyboardAccelerators();
    }

    public Visibility BooleanToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
    public Visibility InverseBooleanToVisibility(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    private async Task<string?> PromptPasswordAsync()
    {
        var dialog = new PasswordDialog { XamlRoot = this.XamlRoot };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? dialog.Password : null;
    }

    private async Task<ConflictResolution> PromptConflictAsync(string existingFilePath)
    {
        var dialog = new ConflictDialog(existingFilePath) { XamlRoot = this.XamlRoot };
        await dialog.ShowAsync();
        return dialog.Resolution;
    }

    private async Task<bool> PromptDeleteConfirmationAsync(string prompt)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = "Confirm Deletion",
            Content = prompt,
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private void ShowNotification(string title, string message)
    {
        NotificationBar.Title = title;
        NotificationBar.Message = message;
        NotificationBar.IsOpen = true;
    }

    private async Task ShowErrorAsync(string title, string technicalMessage, string? technicalDetails)
    {
        var userMessage = ErrorMessageFormatter.Format(title, technicalMessage);
        var details = string.IsNullOrWhiteSpace(technicalDetails) ? technicalMessage : technicalDetails;
        var dialog = new ErrorDialog(title, userMessage, details) { XamlRoot = XamlRoot };
        await dialog.ShowAsync();
    }

    private void RegisterKeyboardAccelerators()
    {
        var find = new KeyboardAccelerator { Key = VirtualKey.F, Modifiers = VirtualKeyModifiers.Control };
        find.Invoked += (_, args) =>
        {
            SearchBox.Focus(FocusState.Programmatic);
            args.Handled = true;
        };
        KeyboardAccelerators.Add(find);

        var selectAll = new KeyboardAccelerator { Key = VirtualKey.A, Modifiers = VirtualKeyModifiers.Control };
        selectAll.Invoked += (_, args) =>
        {
            if (!ViewModel.HasArchive || ViewModel.IsBusy)
                return;

            ItemsListView.SelectAll();
            args.Handled = true;
        };
        ItemsListView.KeyboardAccelerators.Add(selectAll);

        var deleteKey = new KeyboardAccelerator { Key = VirtualKey.Delete };
        deleteKey.Invoked += (_, args) =>
        {
            if (!ViewModel.HasArchive || ViewModel.IsBusy)
                return;

            args.Handled = true;
            OnDeleteClick(this, null!);
        };
        ItemsListView.KeyboardAccelerators.Add(deleteKey);

        var copy = new KeyboardAccelerator { Key = VirtualKey.C, Modifiers = VirtualKeyModifiers.Control };
        copy.Invoked += (_, args) =>
        {
            var paths = ItemsListView.SelectedItems.OfType<ArchiveItemViewModel>()
                .Where(item => !item.IsParentFolderLink)
                .Select(item => item.FullPath)
                .ToArray();
            if (paths.Length == 0)
                return;

            var package = new DataPackage();
            package.SetText(string.Join(Environment.NewLine, paths));
            Clipboard.SetContent(package);
            args.Handled = true;
        };
        ItemsListView.KeyboardAccelerators.Add(copy);

        var goUp = new KeyboardAccelerator { Key = VirtualKey.Back };
        goUp.Invoked += (_, args) =>
        {
            if (ViewModel.HasArchive && !ViewModel.IsBusy && !string.IsNullOrEmpty(ViewModel.CurrentFolderPath))
            {
                ViewModel.NavigateUp();
                args.Handled = true;
            }
        };
        ItemsListView.KeyboardAccelerators.Add(goUp);

        var cancel = new KeyboardAccelerator { Key = VirtualKey.Escape };
        cancel.Invoked += (_, args) =>
        {
            if (ViewModel.IsBusy)
            {
                ViewModel.CancelOperation();
                args.Handled = true;
            }
        };
        KeyboardAccelerators.Add(cancel);
    }

    private async void OnOpenArchiveClick(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.SuggestedStartLocation = PickerLocationId.Desktop;
        foreach (var extension in SupportedArchiveExtensions)
        {
            picker.FileTypeFilter.Add(extension);
        }

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow.Current);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            await ViewModel.OpenArchiveAsync(file.Path);
        }
    }

    private async void OnNewArchiveClick(object sender, RoutedEventArgs e)
    {
        // Pick files to compress
        var picker = new FileOpenPicker();
        picker.SuggestedStartLocation = PickerLocationId.Desktop;
        picker.FileTypeFilter.Add("*");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow.Current);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var files = await picker.PickMultipleFilesAsync();
        if (files == null || files.Count == 0) return;

        var sourcePaths = files.Select(f => f.Path).ToList();
        var defaultName = files.Count == 1
            ? Path.GetFileNameWithoutExtension(files[0].Path)
            : "Archive";

        var defaultFolder = Path.GetDirectoryName(files[0].Path) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        var dialog = new CompressDialog(defaultName, defaultFolder) { XamlRoot = this.XamlRoot };
        var res = await dialog.ShowAsync();

        if (res == ContentDialogResult.Primary)
        {
            await ViewModel.CreateArchiveAsync(
                dialog.OutputFilePath,
                dialog.SelectedFormat,
                sourcePaths,
                dialog.Options);
        }
    }

    private async void OnExtractAllClick(object sender, RoutedEventArgs e)
    {
        await ExtractAllWithDialogAsync();
    }

    public async Task ExtractAllWithDialogAsync()
    {
        if (ViewModel.CurrentArchive == null) return;

        var dialog = new ExtractDialog(ViewModel.CurrentArchive.FilePath) { XamlRoot = this.XamlRoot };
        var res = await dialog.ShowAsync();

        if (res == ContentDialogResult.Primary)
        {
            var destDir = dialog.CreateSubfolder
                ? Path.Combine(dialog.DestinationPath, Path.GetFileNameWithoutExtension(ViewModel.CurrentArchive.FilePath))
                : dialog.DestinationPath;

            await ViewModel.ExtractAllAsync(destDir, dialog.SelectedConflictPolicy, dialog.OpenFolderAfter);
        }
    }

    private async void OnExtractSelectedClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CurrentArchive == null) return;

        var selected = ItemsListView.SelectedItems.Cast<ArchiveItemViewModel>().ToList();
        if (selected.Count == 0)
        {
            OnExtractAllClick(sender, e);
            return;
        }

        var dialog = new ExtractDialog(ViewModel.CurrentArchive.FilePath) { XamlRoot = this.XamlRoot };
        var res = await dialog.ShowAsync();

        if (res == ContentDialogResult.Primary)
        {
            var destDir = dialog.CreateSubfolder
                ? Path.Combine(dialog.DestinationPath, Path.GetFileNameWithoutExtension(ViewModel.CurrentArchive.FilePath))
                : dialog.DestinationPath;

            await ViewModel.ExtractSelectedAsync(selected, destDir, dialog.SelectedConflictPolicy);
        }
    }

    private async void OnExtractToFolderClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CurrentArchive == null) return;

        var baseDir = Path.GetDirectoryName(ViewModel.CurrentArchive.FilePath) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var subDir = Path.Combine(baseDir, Path.GetFileNameWithoutExtension(ViewModel.CurrentArchive.FilePath));

        await ViewModel.ExtractAllAsync(subDir, ConflictPolicy.AskUser, true);
    }

    private async void OnTestArchiveClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.TestArchiveAsync();
    }

    private async void OnAddFilesClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CurrentArchive == null) return;

        var picker = new FileOpenPicker();
        picker.SuggestedStartLocation = PickerLocationId.Desktop;
        picker.FileTypeFilter.Add("*");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow.Current);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var files = await picker.PickMultipleFilesAsync();
        if (files == null || files.Count == 0) return;

        var sourcePaths = files.Select(f => f.Path).ToList();
        await ViewModel.AddFilesToArchiveAsync(sourcePaths);
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CurrentArchive == null) return;
        var selected = ItemsListView.SelectedItems.Cast<ArchiveItemViewModel>().ToList();
        await ViewModel.DeleteSelectedEntriesAsync(selected);
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        ViewModel.RefreshDisplayItems();
    }

    private async void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsDialog { XamlRoot = this.XamlRoot };
        await dialog.ShowAsync();
    }

    private async void OnCheckForUpdatesMenuClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsDialog { XamlRoot = this.XamlRoot };
        await dialog.ShowAsync();
    }

    private async void OnAboutClick(object sender, RoutedEventArgs e)
    {
        var aboutDialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = "About Firezip",
            Content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock { Text = "Firezip 1.0 (x64 Native)", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 16 },
                    new TextBlock { Text = "A 100% free, fast, and secure Windows archive manager built with .NET 10 and WinUI 3.", TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = "Features: No ads, no telemetry, no subscriptions, fully offline, streaming engine with Zip Slip protection.", TextWrapping = TextWrapping.Wrap, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] }
                }
            },
            CloseButtonText = "OK"
        };
        await aboutDialog.ShowAsync();
    }

    private void OnUpClick(object sender, RoutedEventArgs e)
    {
        ViewModel.NavigateUp();
    }

    private void OnCloseArchiveClick(object sender, RoutedEventArgs e)
    {
        ViewModel.CloseArchive();
    }

    private async void OnItemDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ItemsListView.SelectedItem is ArchiveItemViewModel item)
        {
            if (item.IsParentFolderLink)
            {
                ViewModel.NavigateUp();
            }
            else if (item.IsDirectory)
            {
                ViewModel.NavigateToFolder(item.FullPath);
            }
            else
            {
                await ViewModel.OpenFileItemAsync(item);
            }
        }
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = ItemsListView.SelectedItems.Cast<ArchiveItemViewModel>();
        ViewModel.UpdateSelection(selected);
    }

    private void OnCancelOperationClick(object sender, RoutedEventArgs e)
    {
        ViewModel.CancelOperation();
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        Application.Current.Exit();
    }

    private async void OnContextOpenClick(object sender, RoutedEventArgs e)
    {
        if (ItemsListView.SelectedItem is ArchiveItemViewModel item)
        {
            if (item.IsDirectory)
            {
                ViewModel.NavigateToFolder(item.FullPath);
            }
            else
            {
                await ViewModel.OpenFileItemAsync(item);
            }
        }
    }

    private void OnContextCopyNameClick(object sender, RoutedEventArgs e)
    {
        if (ItemsListView.SelectedItem is ArchiveItemViewModel item)
        {
            var package = new DataPackage();
            package.SetText(item.Name);
            Clipboard.SetContent(package);
        }
    }

    private void OnContextCopyPathClick(object sender, RoutedEventArgs e)
    {
        if (ItemsListView.SelectedItem is ArchiveItemViewModel item)
        {
            var package = new DataPackage();
            package.SetText(item.FullPath);
            Clipboard.SetContent(package);
        }
    }

    private void OnArchiveItemsDragStarting(object sender, DragItemsStartingEventArgs e)
    {
        var selectedItems = e.Items.OfType<ArchiveItemViewModel>()
            .Where(item => !item.IsParentFolderLink)
            .ToList();
        if (selectedItems.Count == 0 || ViewModel.IsBusy)
        {
            e.Cancel = true;
            return;
        }

        e.Data.RequestedOperation = DataPackageOperation.Copy;
        e.Data.Properties.Title = selectedItems.Count == 1
            ? selectedItems[0].Name
            : $"{selectedItems.Count} archive items";
        e.Data.SetDataProvider(StandardDataFormats.StorageItems, async request =>
        {
            var deferral = request.GetDeferral();
            try
            {
                var paths = await ViewModel.MaterializeSelectedItemsForDragAsync(selectedItems);
                var storageItems = new List<IStorageItem>(paths.Count);
                foreach (var path in paths)
                {
                    if (File.Exists(path))
                        storageItems.Add(await StorageFile.GetFileFromPathAsync(path));
                    else if (Directory.Exists(path))
                        storageItems.Add(await StorageFolder.GetFolderFromPathAsync(path));
                }

                request.SetData(storageItems);
            }
            catch (Exception ex)
            {
                request.SetData(new List<IStorageItem>());
                await ShowErrorAsync("Drag operation failed", ex.Message, ex.ToString());
            }
            finally
            {
                deferral.Complete();
            }
        });
    }

    public async Task HandleDropAsync(DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count == 0) return;

            var first = items[0];
            if (first is StorageFile file)
            {
                var ext = Path.GetExtension(file.Path).ToLowerInvariant();
                if (SupportedArchiveExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                {
                    await ViewModel.OpenArchiveAsync(file.Path);
                    return;
                }
            }

            var paths = items.Select(i => i.Path).ToList();

            // If an archive is already open, add dropped files into it
            if (ViewModel.HasArchive)
            {
                await ViewModel.AddFilesToArchiveAsync(paths);
                return;
            }

            // Otherwise, prompt to create a new archive from dropped files
            var defaultName = items.Count == 1 ? Path.GetFileNameWithoutExtension(first.Path) : "Archive";
            var defaultFolder = Path.GetDirectoryName(first.Path) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            var dialog = new CompressDialog(defaultName, defaultFolder) { XamlRoot = this.XamlRoot };
            var res = await dialog.ShowAsync();

            if (res == ContentDialogResult.Primary)
            {
                await ViewModel.CreateArchiveAsync(
                    dialog.OutputFilePath,
                    dialog.SelectedFormat,
                    paths,
                    dialog.Options);
            }
        }
    }
}
