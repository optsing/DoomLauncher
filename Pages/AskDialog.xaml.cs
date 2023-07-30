using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DoomLauncher;
/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class AskDialog : ContentDialog
{
    public string Text { get; set; }

    public AskDialog(XamlRoot root, string title, string text, string primaryButton, string closeButton)
    {
        this.InitializeComponent();
        this.XamlRoot = root;
        this.Title = title;
        this.Text = text;
        this.PrimaryButtonText = primaryButton;
        this.CloseButtonText = closeButton;
    }

    public static async Task<bool> ShowAsync(XamlRoot xamlRoot, string title, string text, string primaryButton, string closeButton)
    {
        var dialog = new AskDialog(xamlRoot, title, text, primaryButton, closeButton);
        return ContentDialogResult.Primary == await dialog.ShowAsync();
    }
}
