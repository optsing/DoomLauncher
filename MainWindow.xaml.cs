using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Text.Json;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DoomLauncher;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window
{
    public readonly IntPtr hWnd;
    public readonly RootPage rootPage;
    public readonly DispatcherQueue dispatcher;

    public MainWindow()
    {
        InitializeComponent();

        hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        dispatcher = DispatcherQueue.GetForCurrentThread();

        AppWindow.Title = "GZDoom Launcher";
        AppWindow.SetIcon("Assets/app.ico");
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            AppWindow.TitleBar.IconShowOptions = IconShowOptions.HideIconAndSystemMenu;
            AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        }

        Closed += MainWindow_Closed;

        try
        {
            // Packaged only
            dataFolderPath = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
        }
        catch
        {
            // Unpackaged only
            dataFolderPath = Directory.GetCurrentDirectory();
        }
        configFilePath = Path.Combine(dataFolderPath, "config.json");

        if (File.Exists(configFilePath))
        {
            var text = File.ReadAllText(configFilePath);
            if (JsonSerializer.Deserialize(text, JsonSettingsContext.Default.Settings) is Settings settings) {
                Settings.Current = settings;
            }
            var backupConfigFilePath = Path.Combine(dataFolderPath, "config.old.json");
            File.Copy(configFilePath, backupConfigFilePath, true);
        }

        if (Settings.Current.WindowX != null && Settings.Current.WindowY != null && Settings.Current.WindowWidth != null && Settings.Current.WindowHeight != null)
        {
            AppWindow.MoveAndResize(new()
            {
                X = (int)Settings.Current.WindowX,
                Y = (int)Settings.Current.WindowY,
                Width = (int)Settings.Current.WindowWidth,
                Height = (int)Settings.Current.WindowHeight,
            });
        }
        if (Settings.Current.WindowMaximized && AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }

        AppWindow.Changed += AppWindow_Changed;

        rootPage = new RootPage(AppWindow, hWnd, dataFolderPath);
        rootPage.OnSave += RootPage_OnSave;
        frameRoot.Content = rootPage;
    }

    private void RootPage_OnSave(object? sender, System.EventArgs e)
    {
        Save();
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (sender.Presenter is OverlappedPresenter presenter)
        {
            if (presenter.State == OverlappedPresenterState.Maximized)
            {
                Settings.Current.WindowMaximized = true;
            }
            else if (presenter.State != OverlappedPresenterState.Minimized)
            {
                Settings.Current.WindowMaximized = false;
                Settings.Current.WindowX = sender.Position.X;
                Settings.Current.WindowY = sender.Position.Y;
                Settings.Current.WindowWidth = sender.Size.Width;
                Settings.Current.WindowHeight = sender.Size.Height;
            }
        }
    }

    private readonly string dataFolderPath;
    private readonly string configFilePath;

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        Save();
    }

    public void Save()
    {
        var text = JsonSerializer.Serialize(Settings.Current, JsonSettingsContext.Default.Settings);
        File.WriteAllText(configFilePath, text);
    }
}
