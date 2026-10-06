using Firezip.Core.Interfaces;
using Firezip.Core.Models;
using Firezip.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Firezip.UI.Views;

public sealed partial class SettingsDialog : ContentDialog
{
    private readonly SettingsViewModel _viewModel;

    public SettingsDialog()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<SettingsViewModel>();

        // Populate controls from ViewModel
        OpenFolderCheckBox.IsChecked = _viewModel.OpenFolderAfterExtraction;
        ConfirmOverwriteCheckBox.IsChecked = _viewModel.ConfirmBeforeOverwriting;
        ConfirmDeleteCheckBox.IsChecked = _viewModel.ConfirmBeforeDeleting;
        AutoCloseProgressCheckBox.IsChecked = _viewModel.AutoCloseTaskProgressWindow;
        NotifyCompletionCheckBox.IsChecked = _viewModel.NotifyOnTaskCompletion;
        ContextMenuCheckBox.IsChecked = _viewModel.EnableContextMenu;
        CascadingMenuCheckBox.IsChecked = _viewModel.UseCascadingContextMenu;
        OpenWithCheckBox.IsChecked = _viewModel.EnableOpenWith;
        ExtractHereCheckBox.IsChecked = _viewModel.EnableExtractHere;
        ExtractToFolderCheckBox.IsChecked = _viewModel.EnableExtractToFolder;
        ExtractToCheckBox.IsChecked = _viewModel.EnableExtractTo;
        CompressToZipCheckBox.IsChecked = _viewModel.EnableCompressToZip;
        CompressTo7zCheckBox.IsChecked = _viewModel.EnableCompressTo7z;
        ContextMenuEntriesPanel.IsEnabled = _viewModel.EnableContextMenu;
        CascadingMenuCheckBox.IsEnabled = _viewModel.EnableContextMenu;

        DefaultFormatBox.SelectedIndex = _viewModel.DefaultArchiveFormat switch
        {
            ArchiveFormat.SevenZip => 1,
            ArchiveFormat.Tar => 2,
            _ => 0
        };

        DefaultLevelBox.SelectedIndex = _viewModel.DefaultCompressionLevel switch
        {
            CompressionLevel.Store => 0,
            CompressionLevel.Fast => 1,
            CompressionLevel.Normal => 2,
            CompressionLevel.Maximum => 3,
            CompressionLevel.Ultra => 4,
            _ => 2
        };

        AutoCheckUpdatesCheckBox.IsChecked = _viewModel.AutoCheckUpdates;
        UpdateFrequencyBox.SelectedIndex = _viewModel.UpdateCheckFrequency switch
        {
            "Weekly" => 1,
            "Monthly" => 2,
            _ => 0
        };
        LanguageComboBox.SelectedIndex = _viewModel.Language switch
        {
            "en-US" => 1,
            "pt-BR" => 2,
            _ => 0
        };

        CurrentVersionTextBlock.Text = $"Versão atual: v{_viewModel.CurrentVersion}";
        LastCheckTextBlock.Text = $"Última verificação: {_viewModel.LastCheckDisplay}";

        PrimaryButtonClick += async (s, e) =>
        {
            _viewModel.Language = LanguageComboBox.SelectedIndex switch
            {
                1 => "en-US",
                2 => "pt-BR",
                _ => "System"
            };
            _viewModel.OpenFolderAfterExtraction = OpenFolderCheckBox.IsChecked ?? true;
            _viewModel.ConfirmBeforeOverwriting = ConfirmOverwriteCheckBox.IsChecked ?? true;
            _viewModel.ConfirmBeforeDeleting = ConfirmDeleteCheckBox.IsChecked ?? true;
            _viewModel.AutoCloseTaskProgressWindow = AutoCloseProgressCheckBox.IsChecked ?? true;
            _viewModel.NotifyOnTaskCompletion = NotifyCompletionCheckBox.IsChecked ?? true;
            _viewModel.EnableContextMenu = ContextMenuCheckBox.IsChecked ?? true;
            _viewModel.UseCascadingContextMenu = CascadingMenuCheckBox.IsChecked ?? false;
            _viewModel.EnableOpenWith = OpenWithCheckBox.IsChecked ?? true;
            _viewModel.EnableExtractHere = ExtractHereCheckBox.IsChecked ?? true;
            _viewModel.EnableExtractToFolder = ExtractToFolderCheckBox.IsChecked ?? true;
            _viewModel.EnableExtractTo = ExtractToCheckBox.IsChecked ?? true;
            _viewModel.EnableCompressToZip = CompressToZipCheckBox.IsChecked ?? true;
            _viewModel.EnableCompressTo7z = CompressTo7zCheckBox.IsChecked ?? true;
            _viewModel.AutoCheckUpdates = AutoCheckUpdatesCheckBox.IsChecked ?? true;
            _viewModel.UpdateCheckFrequency = UpdateFrequencyBox.SelectedIndex switch
            {
                1 => "Weekly",
                2 => "Monthly",
                _ => "Daily"
            };

            _viewModel.DefaultArchiveFormat = DefaultFormatBox.SelectedIndex switch
            {
                1 => ArchiveFormat.SevenZip,
                2 => ArchiveFormat.Tar,
                _ => ArchiveFormat.Zip
            };

            _viewModel.DefaultCompressionLevel = DefaultLevelBox.SelectedIndex switch
            {
                0 => CompressionLevel.Store,
                1 => CompressionLevel.Fast,
                2 => CompressionLevel.Normal,
                3 => CompressionLevel.Maximum,
                4 => CompressionLevel.Ultra,
                _ => CompressionLevel.Normal
            };

            await _viewModel.SaveSettingsAsync();
        };
    }

    private async void OnCheckUpdatesClick(object sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false;
        UpdateProgressRing.Visibility = Visibility.Visible;
        UpdateProgressRing.IsActive = true;
        UpdateStatusTextBlock.Visibility = Visibility.Visible;
        UpdateStatusTextBlock.Text = "Verificando atualizações...";

        try
        {
            await _viewModel.CheckForUpdatesNowAsync();
            UpdateStatusTextBlock.Text = _viewModel.UpdateStatusMessage;
            LastCheckTextBlock.Text = $"Última verificação: {_viewModel.LastCheckDisplay}";

            if (_viewModel.IsUpdateAvailable)
            {
                ApplyUpdateButton.Visibility = Visibility.Visible;
            }
            else
            {
                ApplyUpdateButton.Visibility = Visibility.Collapsed;
            }
        }
        finally
        {
            UpdateProgressRing.IsActive = false;
            UpdateProgressRing.Visibility = Visibility.Collapsed;
            CheckUpdatesButton.IsEnabled = true;
        }
    }

    private async void OnApplyUpdateClick(object sender, RoutedEventArgs e)
    {
        ApplyUpdateButton.IsEnabled = false;
        UpdateStatusTextBlock.Text = "Iniciando instalador da nova versão...";
        await _viewModel.ApplyUpdateAsync();
    }

    private void OnContextMenuMasterChanged(object sender, RoutedEventArgs e)
    {
        var isEnabled = ContextMenuCheckBox.IsChecked == true;
        ContextMenuEntriesPanel.IsEnabled = isEnabled;
        CascadingMenuCheckBox.IsEnabled = isEnabled;
    }

    private void OnRegisterAssociationsClick(object sender, RoutedEventArgs e)
    {
        _viewModel.RegisterFileAssociations();
        ShowStatus("Firezip is available in Open With. Choose it in Windows Settings to make it your default archive app.");
    }

    private void OnUnregisterAssociationsClick(object sender, RoutedEventArgs e)
    {
        _viewModel.UnregisterFileAssociations();
        ShowStatus("Removed file associations.");
    }

    private void OnOpenLogFolderClick(object sender, RoutedEventArgs e)
    {
        _viewModel.OpenLogFolder();
    }

    private void ShowStatus(string message)
    {
        StatusTextBlock.Text = message;
        StatusTextBlock.Visibility = Visibility.Visible;
    }
}
