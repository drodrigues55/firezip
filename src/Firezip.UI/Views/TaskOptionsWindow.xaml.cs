using Firezip.Core.Interfaces;
using Firezip.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Firezip.UI.Views;

public sealed partial class TaskOptionsWindow : Window
{
    private readonly string _archivePath;
    private bool _transitionedToProgress;

    public TaskOptionsWindow(string archivePath)
    {
        InitializeComponent();
        _archivePath = archivePath;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        ConfigureWindowSizeAndPosition();

        var fileName = Path.GetFileName(_archivePath);
        ArchiveNameTextBlock.Text = fileName;
        Title = $"Extract {fileName} — Firezip";

        var settings = App.Services.GetRequiredService<ISettingsService>();
        var defaultFolder = !string.IsNullOrWhiteSpace(settings.DefaultExtractionFolder)
            ? settings.DefaultExtractionFolder
            : Path.GetDirectoryName(_archivePath) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        DestinationPathBox.Text = defaultFolder;
        OpenFolderCheckBox.IsChecked = settings.OpenExtractedFolderAfterExtraction;

        Closed += (_, _) =>
        {
            if (!_transitionedToProgress)
            {
                Application.Current.Exit();
            }
        };
    }

    private void ConfigureWindowSizeAndPosition()
    {
        try
        {
            AppWindow.SetIcon("Assets/AppIcon.ico");
        }
        catch { }

        const int width = 520;
        const int height = 370;
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

    private async void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var folderPicker = new FolderPicker();
        folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;
        folderPicker.FileTypeFilter.Add("*");

        var hwnd = WindowNative.GetWindowHandle(this);
        InitializeWithWindow.Initialize(folderPicker, hwnd);

        var folder = await folderPicker.PickSingleFolderAsync();
        if (folder != null)
        {
            DestinationPathBox.Text = folder.Path;
        }
    }

    private void OnExtractClick(object sender, RoutedEventArgs e)
    {
        var baseDir = DestinationPathBox.Text.Trim();
        if (string.IsNullOrEmpty(baseDir))
        {
            baseDir = Path.GetDirectoryName(_archivePath) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }

        var targetDir = SubfolderCheckBox.IsChecked == true
            ? Path.Combine(baseDir, Path.GetFileNameWithoutExtension(_archivePath))
            : baseDir;

        var policy = ConflictPolicyBox.SelectedIndex switch
        {
            1 => ConflictPolicy.Overwrite,
            2 => ConflictPolicy.Skip,
            3 => ConflictPolicy.Rename,
            _ => ConflictPolicy.AskUser
        };

        var openFolderAfter = OpenFolderCheckBox.IsChecked ?? true;

        _transitionedToProgress = true;
        var progressWindow = new TaskProgressWindow(
            TaskProgressWindow.TaskType.Extract,
            _archivePath,
            targetDir,
            policy,
            openFolderAfter);

        progressWindow.Activate();
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
