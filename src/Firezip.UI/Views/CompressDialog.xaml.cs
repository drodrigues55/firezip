using Firezip.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace Firezip.UI.Views;

public sealed partial class CompressDialog : ContentDialog
{
    public ArchiveFormat SelectedFormat => FormatComboBox.SelectedIndex switch
    {
        1 => ArchiveFormat.SevenZip,
        2 => ArchiveFormat.Tar,
        3 => ArchiveFormat.GZip,
        _ => ArchiveFormat.Zip
    };

    public CompressionLevel SelectedLevel => LevelComboBox.SelectedIndex switch
    {
        0 => CompressionLevel.Store,
        1 => CompressionLevel.Fast,
        2 => CompressionLevel.Normal,
        3 => CompressionLevel.Maximum,
        4 => CompressionLevel.Ultra,
        _ => CompressionLevel.Normal
    };

    public string OutputFilePath
    {
        get
        {
            var ext = SelectedFormat.GetDefaultExtension();
            var name = ArchiveNameBox.Text.Trim();
            if (!name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            {
                name += ext;
            }
            return Path.Combine(DestinationFolderBox.Text.Trim(), name);
        }
    }

    public CompressionOptions Options => new()
    {
        Level = SelectedLevel,
        PreserveTimestamps = PreserveTimestampsCheckBox.IsChecked ?? true,
        Password = (SelectedFormat is ArchiveFormat.Zip or ArchiveFormat.SevenZip) &&
                   !string.IsNullOrWhiteSpace(PasswordBox.Password)
            ? PasswordBox.Password
            : null,
        EncryptHeader = SelectedFormat == ArchiveFormat.SevenZip && (EncryptHeaderCheckBox.IsChecked ?? false),
        SplitArchive = SelectedFormat == ArchiveFormat.SevenZip && SplitVolumeBox.SelectedIndex > 0,
        VolumeSizeBytes = SelectedFormat == ArchiveFormat.SevenZip ? SplitVolumeBox.SelectedIndex switch
        {
            1 => 10L * 1024 * 1024,
            2 => 100L * 1024 * 1024,
            3 => 700L * 1024 * 1024,
            4 => 4700L * 1024 * 1024,
            _ => null
        } : null,
        ExcludePatterns = ExcludePatternsBox.Text
            .Split([';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    };

    public CompressDialog(string? initialName = null, string? initialFolder = null)
    {
        InitializeComponent();

        if (!string.IsNullOrWhiteSpace(initialName))
        {
            ArchiveNameBox.Text = Path.GetFileNameWithoutExtension(initialName);
        }

        DestinationFolderBox.Text = !string.IsNullOrWhiteSpace(initialFolder)
            ? initialFolder
            : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        FormatComboBox.SelectionChanged += (_, _) => UpdateFormatControls();
        UpdateFormatControls();
    }

    private void UpdateFormatControls()
    {
        var isSevenZip = SelectedFormat == ArchiveFormat.SevenZip;
        var supportsConfigurableLevel = SelectedFormat is ArchiveFormat.Zip or ArchiveFormat.SevenZip;
        PasswordBox.IsEnabled = SelectedFormat is ArchiveFormat.Zip or ArchiveFormat.SevenZip;
        LevelComboBox.IsEnabled = supportsConfigurableLevel;
        EncryptHeaderCheckBox.IsEnabled = isSevenZip;
        SplitVolumeBox.IsEnabled = isSevenZip;

        if (!isSevenZip)
        {
            EncryptHeaderCheckBox.IsChecked = false;
            SplitVolumeBox.SelectedIndex = 0;
        }

        if (!supportsConfigurableLevel)
            LevelComboBox.SelectedIndex = 2;
    }

    private async void OnBrowseLocationClick(object sender, RoutedEventArgs e)
    {
        var folderPicker = new FolderPicker();
        folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;
        folderPicker.FileTypeFilter.Add("*");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow.Current);
        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

        var folder = await folderPicker.PickSingleFolderAsync();
        if (folder != null)
        {
            DestinationFolderBox.Text = folder.Path;
        }
    }
}
