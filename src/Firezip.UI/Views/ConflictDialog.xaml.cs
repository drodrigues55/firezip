using Firezip.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Firezip.UI.Views;

public sealed partial class ConflictDialog : ContentDialog
{
    public ConflictResolution Resolution { get; private set; } = ConflictResolution.CancelOperation;

    public ConflictDialog(string existingFilePath)
    {
        InitializeComponent();
        var fileName = Path.GetFileName(existingFilePath);
        MessageBlock.Text = $"The file '{fileName}' already exists in the destination folder. How would you like to proceed?";
    }

    private void OnReplaceClick(object sender, RoutedEventArgs e)
    {
        Resolution = new ConflictResolution
        {
            Policy = ConflictPolicy.Overwrite,
            ApplyToAllRemaining = ApplyToAllCheckBox.IsChecked ?? false
        };
        Hide();
    }

    private void OnSkipClick(object sender, RoutedEventArgs e)
    {
        Resolution = new ConflictResolution
        {
            Policy = ConflictPolicy.Skip,
            ApplyToAllRemaining = ApplyToAllCheckBox.IsChecked ?? false
        };
        Hide();
    }

    private void OnRenameClick(object sender, RoutedEventArgs e)
    {
        Resolution = new ConflictResolution
        {
            Policy = ConflictPolicy.Rename,
            ApplyToAllRemaining = ApplyToAllCheckBox.IsChecked ?? false
        };
        Hide();
    }
}
