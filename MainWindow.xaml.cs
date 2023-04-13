using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

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

        SystemBackdrop = new MicaBackdrop();
        //Title = "GZDoom Launcher";
        //ExtendsContentIntoTitleBar = true;
        //SetTitleBar(titleBar);

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
            settings = JsonSerializer.Deserialize<Settings>(text, jsonOptions);
        }
        else
        {
            settings = new()
            {
                GZDoomPath = "",
                Entries = new(),
                SelectedModIndex = 0,
            };
        }

        frameRoot.Content = new RootPage(AppWindow, settings, HWND, dataFolderPath);
    }


    public static readonly string[] SupportedModExtensions = new[] { ".pk3", ".wad", ".zip" };
    public static readonly string[] SupportedImageExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };

    private readonly string dataFolderPath;
    private readonly string configFilePath;

    private readonly Settings settings;

    private readonly JsonSerializerOptions jsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
        Converters = {
            new NamePathConverter(),
            new KeyValueConverter(),
        },
    };

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        Save();
    }

    public void Save()
    {
        var text = JsonSerializer.Serialize<Settings>(settings, jsonOptions);
        File.WriteAllText(configFilePath, text);
    }
}
