using Microsoft.UI.Xaml;
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
    public string PrimaryButton { get; set; }

    public AskDialog(XamlRoot root, string text, string primaryButton)
    {
        this.InitializeComponent();
        this.XamlRoot = root;
        this.Text = text;
        this.PrimaryButton = primaryButton;
    }
}
