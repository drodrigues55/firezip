using Firezip.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace Firezip.UI.Views;

public sealed partial class ExtractDialog : ContentDialog
{
    private readonly string _archivePath;

    public string DestinationPath => DestinationPathBox.Text;
    public bool CreateSubfolder => SubfolderCheckBox.IsChecked ?? true;
    public bool OpenFolderAfter => OpenFolderCheckBox.IsChecked ?? true;

    public ConflictPolicy SelectedConflictPolicy => ConflictPolicyBox.SelectedIndex switch
    {
        1 => ConflictPolicy.Overwrite,
        2 => ConflictPolicy.Skip,
        3 => ConflictPolicy.Rename,
        _ => ConflictPolicy.AskUser
    };

    public ExtractDialog(string archivePath, string? defaultDestination = null)
    {
        InitializeComponent();
        _archivePath = archivePath;

        var baseDir = !string.IsNullOrWhiteSpace(defaultDestination)
            ? defaultDestination
            : Path.GetDirectoryName(archivePath) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        DestinationPathBox.Text = baseDir;
    }

    private async void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var folderPicker = new FolderPicker();
        folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;
        folderPicker.FileTypeFilter.Add("*");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow.Current);
        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

        var folder = await folderPicker.PickSingleFolderAsync();
        if (folder != null)
        {
            DestinationPathBox.Text = folder.Path;
        }
    }
}
