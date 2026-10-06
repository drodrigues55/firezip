using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace Firezip.UI.Views;

public sealed partial class ErrorDialog : ContentDialog
{
    public ErrorDialog(string title, string message, string technicalDetails)
    {
        InitializeComponent();
        Title = title;
        MessageBlock.Text = message;
        DetailsTextBox.Text = technicalDetails;
        DetailsButton.Visibility = string.IsNullOrWhiteSpace(technicalDetails)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void OnDetailsClick(object sender, RoutedEventArgs e)
    {
        var showDetails = DetailsPanel.Visibility != Visibility.Visible;
        DetailsPanel.Visibility = showDetails ? Visibility.Visible : Visibility.Collapsed;
        DetailsButton.Content = showDetails ? "Hide technical details" : "Show technical details";
    }

    private void OnCopyDetailsClick(object sender, RoutedEventArgs e)
    {
        var package = new DataPackage();
        package.SetText(DetailsTextBox.Text);
        Clipboard.SetContent(package);
    }
}
