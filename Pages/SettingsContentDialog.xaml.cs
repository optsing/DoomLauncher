using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Windows.ApplicationModel;

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

    private string appVersion;

    public SettingsContentDialog(XamlRoot root, IntPtr hWnd, SettingsDialogState state)
    {
        this.InitializeComponent();
        this.XamlRoot = root;
        this.hWnd = hWnd;
        this.State = state;
        if (Settings.ValidateGZDoomPath(State.GZDoomPath))
        {
            State.IsGZDoomPathValid = Visibility.Visible;
            State.GZDoomVersion = GetFileVersion(State.GZDoomPath) is string version ? "Выбрана версия " + version : "Выбрана неизвестная версия";
        }
        else
        {
            State.IsGZDoomPathValid = Visibility.Collapsed;
            State.GZDoomVersion = "Выберите исполняемый файл";
        }
        try
        {
            var version = Package.Current.Id.Version;
            appVersion = $"Версия {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
        catch {
            if (Assembly.GetExecutingAssembly()?.GetName()?.Version is Version version)
            {
                appVersion = "Версия " + version.ToString();
            }
            else
            {
                appVersion = "Неизвестная версия";
            }
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
        if (file != null && Settings.ValidateGZDoomPath(file.Path))
        {
            State.GZDoomPath = file.Path;
            State.IsGZDoomPathValid = Visibility.Visible;
            State.GZDoomVersion = GetFileVersion(file.Path) is string version ? "Выбрана версия " + version : "Выбрана неизвестная версия";
        }
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        await ChooseGZDoomPath();
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
            args.Cancel = !Settings.ValidateGZDoomPath(State.GZDoomPath);
    }

    private static string? GetFileVersion(string filePath)
    {
        return FileVersionInfo.GetVersionInfo(filePath)?.ProductVersion;
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        Process.Start("explorer.exe", "/select," + State.GZDoomPath);
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

    private Visibility isGZDoomPathValid = Visibility.Collapsed;

    public Visibility IsGZDoomPathValid
    {
        get => isGZDoomPathValid;
        set => SetProperty(ref isGZDoomPathValid, value);
    }

    public bool CloseOnLaunch { get; set; } = false;
}
