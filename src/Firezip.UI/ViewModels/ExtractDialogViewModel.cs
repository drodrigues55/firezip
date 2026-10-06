using CommunityToolkit.Mvvm.ComponentModel;
using Firezip.Core.Models;

namespace Firezip.UI.ViewModels;

public partial class ExtractDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _destinationDirectory = string.Empty;

    [ObservableProperty]
    private bool _createSubfolder = true;

    [ObservableProperty]
    private bool _openFolderAfterExtraction = true;

    [ObservableProperty]
    private ConflictPolicy _selectedConflictPolicy = ConflictPolicy.AskUser;

    public string EffectiveDestinationDirectory(string archiveFilePath)
    {
        var baseDir = string.IsNullOrWhiteSpace(DestinationDirectory)
            ? Path.GetDirectoryName(archiveFilePath) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            : DestinationDirectory;

        if (CreateSubfolder)
        {
            var folderName = Path.GetFileNameWithoutExtension(archiveFilePath);
            return Path.Combine(baseDir, folderName);
        }

        return baseDir;
    }
}
