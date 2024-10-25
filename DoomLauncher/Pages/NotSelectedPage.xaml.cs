using DoomLauncher.Helpers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DoomLauncher;
/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class NotSelectedPage : Page
{
    public NotSelectedPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        EventBus.ChangeBackground(null, AnimationDirection.None);
        EventBus.ChangeCaption(null);
        base.OnNavigatedTo(e);
    }
}
