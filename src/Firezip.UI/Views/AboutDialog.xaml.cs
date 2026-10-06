using System.Reflection;
using Firezip.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace Firezip.UI.Views;

public sealed partial class AboutDialog : ContentDialog
{
    public AboutDialog()
    {
        InitializeComponent();

        var asmVer = Assembly.GetExecutingAssembly().GetName().Version;
        var versionStr = asmVer != null ? $"{asmVer.Major}.{asmVer.Minor}.{asmVer.Build}" : "1.0.1";
        VersionBadgeTextBlock.Text = $"v{versionStr}";

        var loc = App.Services?.GetService<ILocalizationService>();
        if (loc != null)
        {
            Title = loc.GetString("Action_About") + " Firezip";
            CloseButtonText = loc.GetString("Action_Close");
            TaglineTextBlock.Text = string.Equals(loc.CurrentCulture, "pt-BR", StringComparison.OrdinalIgnoreCase)
                ? "O compactador veloz, moderno e nativo para Windows 11."
                : "The ultra-fast, modern, native archive manager for Windows 11.";
        }
    }
}
