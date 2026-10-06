using CommunityToolkit.Mvvm.ComponentModel;
using Firezip.Core.Models;

namespace Firezip.UI.ViewModels;

public partial class CompressDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _archiveName = "Archive";

    [ObservableProperty]
    private string _destinationDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

    [ObservableProperty]
    private ArchiveFormat _selectedFormat = ArchiveFormat.Zip;

    [ObservableProperty]
    private CompressionLevel _selectedLevel = CompressionLevel.Normal;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _encryptHeader;

    [ObservableProperty]
    private bool _splitArchive;

    [ObservableProperty]
    private int _selectedVolumeOption = 0;

    public string FullDestinationPath
    {
        get
        {
            var ext = SelectedFormat.GetDefaultExtension();
            var name = ArchiveName.EndsWith(ext, StringComparison.OrdinalIgnoreCase)
                ? ArchiveName
                : ArchiveName + ext;
            return Path.Combine(DestinationDirectory, name);
        }
    }

    public CompressionOptions ToCompressionOptions()
    {
        long? volumeSize = SelectedVolumeOption switch
        {
            1 => 10L * 1024 * 1024,
            2 => 100L * 1024 * 1024,
            3 => 700L * 1024 * 1024,
            4 => 4700L * 1024 * 1024,
            _ => null
        };

        return new CompressionOptions
        {
            Level = SelectedLevel,
            Password = string.IsNullOrWhiteSpace(Password) ? null : Password,
            EncryptHeader = EncryptHeader,
            SplitArchive = SplitArchive && volumeSize.HasValue,
            VolumeSizeBytes = volumeSize
        };
    }
}
