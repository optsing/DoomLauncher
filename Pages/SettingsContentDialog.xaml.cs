using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DoomLauncher;
/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class SettingsContentDialog : ContentDialog, INotifyPropertyChanged
{
    private readonly IntPtr hWnd;
    private string gzDoomPath;
    private bool closeOnLaunch;

    public SettingsContentDialog(XamlRoot root, IntPtr hWnd, string gzDoomPath, bool closeOnLaunch)
    {
        this.InitializeComponent();
        this.XamlRoot = root;
        this.hWnd = hWnd;
        this.gzDoomPath = gzDoomPath;
        this.closeOnLaunch = closeOnLaunch;
    }

    public string GZDoomPath
    {
        get => gzDoomPath;
        set
        {
            if (gzDoomPath != value)
            {
                gzDoomPath = value;
                OnPropertyChanged(nameof(GZDoomPath));
            }
        }
    }

    public bool CloseOnLaunch
    {
        get => closeOnLaunch;
        set
        {
            if (closeOnLaunch != value)
            {
                closeOnLaunch = value;
                OnPropertyChanged(nameof(CloseOnLaunch));
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
        if (file != null)
        {
            GZDoomPath = file.Path;
        }
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        await ChooseGZDoomPath();
    }

    private void SettingsContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (!Settings.ValidateGZDoomPath(GZDoomPath))
        {
            tbGZDoomPath.Focus(FocusState.Programmatic);
            args.Cancel = true;
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
