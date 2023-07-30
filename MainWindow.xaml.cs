using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
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
    public MainWindow()
    {
        InitializeComponent();

        var HWND = WinRT.Interop.WindowNative.GetWindowHandle(this);

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
            settings = JsonSerializer.Deserialize<Settings>(text, Settings.jsonOptions);
            var backupConfigFilePath = Path.Combine(dataFolderPath, "config.old.json");
            File.Copy(configFilePath, backupConfigFilePath, true);
        }
        else
        {
            settings = new();
        }

        if (settings.WindowX != null && settings.WindowY != null && settings.WindowWidth != null && settings.WindowHeight != null)
        {
            AppWindow.MoveAndResize(new()
            {
                X = (int)settings.WindowX,
                Y = (int)settings.WindowY,
                Width = (int)settings.WindowWidth,
                Height = (int)settings.WindowHeight,
            });
        }
        if (settings.WindowMaximized)
        {
            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.Maximize();
            }
        }

        AppWindow.Changed += AppWindow_Changed;

        frameRoot.Content = new RootPage(AppWindow, settings, HWND, dataFolderPath);
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (sender.Presenter is OverlappedPresenter presenter)
        {
            if (presenter.State == OverlappedPresenterState.Maximized)
            {
                settings.WindowMaximized = true;
            }
            else if (presenter.State != OverlappedPresenterState.Minimized)
            {
                settings.WindowMaximized = false;
                settings.WindowX = sender.Position.X;
                settings.WindowY = sender.Position.Y;
                settings.WindowWidth = sender.Size.Width;
                settings.WindowHeight = sender.Size.Height;
            }
        }
    }

    private readonly string dataFolderPath;
    private readonly string configFilePath;

    private readonly Settings settings;

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        Save();
    }

    public void Save()
    {
        var text = JsonSerializer.Serialize<Settings>(settings, Settings.jsonOptions);
        File.WriteAllText(configFilePath, text);
    }
}
