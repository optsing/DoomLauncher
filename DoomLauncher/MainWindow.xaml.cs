using DoomLauncher.Helpers;
using DoomLauncher.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System.IO;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DoomLauncher;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window
{ 
    public MainWindow()
    {
        WinApi.HWND = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinApi.WindowId = AppWindow.Id;

        AppWindow.Title = "Doom Launcher";
        AppWindow.SetIcon("Assets/app.ico");
        SettingsViewModel.IsCustomTitleBar = AppWindowTitleBar.IsCustomizationSupported();
        if (SettingsViewModel.IsCustomTitleBar)
        {
            AppWindow.TitleBar.IconShowOptions = IconShowOptions.HideIconAndSystemMenu;
            AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }

#if IS_NON_PACKAGED
        var dataFolderPath = Directory.GetCurrentDirectory();
#else
        var dataFolderPath = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
#endif

        FileHelper.ConfigFilePath = Path.Combine(dataFolderPath, "config.json");
        FileHelper.PackagesFolderPath = Path.Combine(dataFolderPath, "gzdoom");
        FileHelper.IWadFolderPath = Path.Combine(dataFolderPath, "iwads");
        FileHelper.ModsFolderPath = Path.Combine(dataFolderPath, "mods");
        FileHelper.ImagesFolderPath = Path.Combine(dataFolderPath, "images");
        FileHelper.EntriesFolderPath = Path.Combine(dataFolderPath, "entries");

        if (File.Exists(FileHelper.ConfigFilePath))
        {
            if (SettingsViewModel.Load() is SettingsViewModel settings)
            {
                SettingsViewModel.Current = settings;
            }
            var backupConfigFilePath = Path.Combine(dataFolderPath, "config.old.json");
            File.Copy(FileHelper.ConfigFilePath, backupConfigFilePath, true);
        }

        if (SettingsViewModel.Current.WindowX != null && SettingsViewModel.Current.WindowY != null && SettingsViewModel.Current.WindowWidth != null && SettingsViewModel.Current.WindowHeight != null)
        {
            AppWindow.MoveAndResize(new()
            {
                X = (int)SettingsViewModel.Current.WindowX,
                Y = (int)SettingsViewModel.Current.WindowY,
                Width = (int)SettingsViewModel.Current.WindowWidth,
                Height = (int)SettingsViewModel.Current.WindowHeight,
            });
        }
        if (SettingsViewModel.Current.WindowMaximized && AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }
        AppWindow.Changed += AppWindow_Changed;
        
        InitializeComponent();
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (sender.Presenter is OverlappedPresenter presenter)
        {
            if (presenter.State == OverlappedPresenterState.Maximized)
            {
                SettingsViewModel.Current.WindowMaximized = true;
            }
            else if (presenter.State == OverlappedPresenterState.Restored)
            {
                SettingsViewModel.Current.WindowMaximized = false;
                SettingsViewModel.Current.WindowX = sender.Position.X;
                SettingsViewModel.Current.WindowY = sender.Position.Y;
                SettingsViewModel.Current.WindowWidth = sender.Size.Width;
                SettingsViewModel.Current.WindowHeight = sender.Size.Height;
            }
        }
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        SettingsViewModel.Current.Save();
    }
}
