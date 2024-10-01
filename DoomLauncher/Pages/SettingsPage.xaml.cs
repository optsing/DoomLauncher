using DoomLauncher.Helpers;
using DoomLauncher.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Text.Json.Serialization;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DoomLauncher;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AssetArch
{
    x64, x86, arm64, x64legacy, x86legacy, unknown, manual, notSelected
}

public sealed partial class SettingsPage : Page
{

    public SettingsPageViewModel ViewModel = new();

    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        EventBus.ChangeBackground(this, null, AnimationDirection.None);
        EventBus.ChangeCaption(this, "Settings");
        base.OnNavigatedTo(e);
    }
}
