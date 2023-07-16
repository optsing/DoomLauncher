using CommunityToolkit.Mvvm.ComponentModel;
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

public sealed partial class SettingsContentDialog : ContentDialog
{
    private readonly IntPtr hWnd;
    private readonly bool forceGZDoomPathSetup;

    public SettingsDialogState State { get; private set; }

    public SettingsContentDialog(XamlRoot root, IntPtr hWnd, SettingsDialogState state, bool forceGZDoomPathSetup)
    {
        this.InitializeComponent();
        this.XamlRoot = root;
        this.hWnd = hWnd;
        this.State = state;
        this.forceGZDoomPathSetup = forceGZDoomPathSetup;
        if (!forceGZDoomPathSetup)
        {
            CloseButtonText = "Отмена";
        }
    }

    private async Task ChooseGZDoomPath()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();

        // Need to initialize the picker object with the hwnd / IInitializeWithWindow 
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

        // Now we can use the picker object as normal
        picker.FileTypeFilter.Add(".exe");
        picker.CommitButtonText = "Выбрать GZDoom";
        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            State.GZDoomPath = file.Path;
        }
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        await ChooseGZDoomPath();
    }

    private void ContentDialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
    {
        if (forceGZDoomPathSetup || ContentDialogResult.Primary == args.Result)
        {
            if (!Settings.ValidateGZDoomPath(State.GZDoomPath))
            {
                tbGZDoomPath.Focus(FocusState.Programmatic);
                args.Cancel = true;
            }
        }
    }
}

public partial class SettingsDialogState: ObservableObject
{
    [ObservableProperty]
    private string gZDoomPath;
    [ObservableProperty]
    private bool closeOnLaunch;
    [ObservableProperty]
    private bool copyFilesToLauncherFolder;
}
