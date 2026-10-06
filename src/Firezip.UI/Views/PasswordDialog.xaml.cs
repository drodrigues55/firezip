using Microsoft.UI.Xaml.Controls;

namespace Firezip.UI.Views;

public sealed partial class PasswordDialog : ContentDialog
{
    public string Password => PasswordInput.Password;

    public PasswordDialog()
    {
        InitializeComponent();
    }
}
