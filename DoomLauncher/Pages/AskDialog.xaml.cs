using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DoomLauncher;
/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class AskDialog : ContentDialog
{
    public string Text { get; set; }

    public AskDialog(string title, string text, string primaryButton, string secondaryButton, string closeButton)
    {
        InitializeComponent();
        Title = title;
        Text = text;
        PrimaryButtonText = primaryButton;
        SecondaryButtonText = secondaryButton;
        CloseButtonText = closeButton;
    }
}
