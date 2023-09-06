using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
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

    public SettingsDialogState State { get; private set; }

    public SettingsContentDialog(XamlRoot root, IntPtr hWnd, SettingsDialogState state)
    {
        this.InitializeComponent();
        this.XamlRoot = root;
        this.hWnd = hWnd;
        this.State = state;
        State.GZDoomVersion = GetGZDoomVersion();
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
            State.GZDoomVersion = GetGZDoomVersion();
        }
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        await ChooseGZDoomPath();
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (!Settings.ValidateGZDoomPath(State.GZDoomPath))
        {
            tbGZDoomPath.Focus(FocusState.Programmatic);
            args.Cancel = true;
        }
    }

    private string GetGZDoomVersion()
    {
        if (Settings.ValidateGZDoomPath(State.GZDoomPath))
        {
            var version = FileVersionInfo.GetVersionInfo(State.GZDoomPath);
            if (version.ProductVersion != null)
            {
                return $"Версия: {version.ProductVersion}";
            }
        }
        return "";
    }
}

public class SettingsDialogState: ObservableObject
{
    private string gZDoomPath = "";
    
    public string GZDoomPath
    {
        get => gZDoomPath;
        set => SetProperty(ref gZDoomPath, value);
    }

    private string gZDoomVersion = "";

    public string GZDoomVersion
    {
        get => gZDoomVersion;
        set => SetProperty(ref gZDoomVersion, value);
    }

    public bool CloseOnLaunch { get; set; } = false;
}
