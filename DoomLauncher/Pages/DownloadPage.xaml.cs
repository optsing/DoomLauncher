using DoomLauncher.Helpers;
using DoomLauncher.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DoomLauncher.Pages;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class DownloadPage : Page
{
    public DownloadPageViewModel ViewModel = new();

    public DownloadPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        EventBus.ChangeBackground(this, null, AnimationDirection.None);
        EventBus.ChangeCaption(this, "Catalog");
        base.OnNavigatedTo(e);

        ViewModel.LoadEntries();
    }
}
