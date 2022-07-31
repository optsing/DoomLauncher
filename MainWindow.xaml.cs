using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
        //Title = "GZDoom Launcher";
        //ExtendsContentIntoTitleBar = true;
        //SetTitleBar(titleBar);

        Closed += MainWindow_Closed;

        HWND = WinRT.Interop.WindowNative.GetWindowHandle(this);
        
        var windowId = Win32Interop.GetWindowIdFromWindow(HWND);
        appWindow = AppWindow.GetFromWindowId(windowId);

        appWindow.Title = "GZDoom Launcher";
        appWindow.SetIcon("Assets/app.ico");
        appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;        

        try
        {
            // Packaged only
            dataFolderPath = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
        }
        catch
        {
            // Unpackaged only
            dataFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "data");
            if (!Directory.Exists(dataFolderPath))
            {
                Directory.CreateDirectory(dataFolderPath);
            }
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

        mainFrame.Content = notSelectedPage;
        DoomList.SelectedIndex = settings.SelectedModIndex;
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        Save();
    }

    private readonly string dataFolderPath;
    private readonly string configFilePath;
    private readonly IntPtr HWND;
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
        Converters = {
            new NamePathConverter(),
            new KeyValueConverter(),
        },
    };

    private readonly Settings settings;

    private readonly NotSelectedPage notSelectedPage = new();

    private readonly AppWindow appWindow;

    private void DoomList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        settings.SelectedModIndex = DoomList.SelectedIndex;
        if (DoomList.SelectedItem is DoomEntry item)
        {
            DoomPage page = new(item, HWND, dataFolderPath);
            page.OnStart += Page_OnStart;
            page.OnEdit += Page_OnEdit;
            mainFrame.Content = page;
        }
        else
        {
            mainFrame.Content = notSelectedPage;
        }
    }

    private async void Page_OnEdit(object sender, DoomEntry e)
    {
        if (await AddOrEditModDialogShow(e.Name, e.IWadFile, true) is EditModDialogResult result) {
            e.Name = result.name;
            e.IWadFile = result.iWadFile;
        }
    }

    private void Page_OnStart(object sender, DoomEntry entry)
    {
        Start(entry);
    }

    private async void Start(DoomEntry entry)
    {
        if (!IsGZDoomPathChoosen())
        {
            var success = await ChooseGZDoomPath();
            if (!success)
            {
                return;
            }
        }
        ProcessStartInfo processInfo = new()
        {
            FileName = settings.GZDoomPath,
            WorkingDirectory = Path.GetDirectoryName(settings.GZDoomPath),
        };
        if (!string.IsNullOrEmpty(entry.IWadFile.Key))
        {
            processInfo.ArgumentList.Add("-iwad");
            processInfo.ArgumentList.Add(entry.IWadFile.Key);
        }
        if (entry.ModFiles.Count > 0)
        {
            processInfo.ArgumentList.Add("-file");
            foreach (var modFile in entry.ModFiles)
            {
                processInfo.ArgumentList.Add(modFile.Path);
            }
        }
        Process.Start(processInfo);
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Minimize();
        }
    }

    public void Save()
    {
        var text = JsonSerializer.Serialize<Settings>(settings, jsonOptions);
        File.WriteAllText(configFilePath, text);
    }

    private async void Button_Click(object sender, RoutedEventArgs e)
    {
        if (await AddOrEditModDialogShow("Новый мод", Settings.IWads.First(), false) is EditModDialogResult result)
        {
            DoomEntry entry = new()
            {
                Id = Guid.NewGuid().ToString(),
                Name = result.name,
                IWadFile = result.iWadFile,
                ModFiles = new(),
            };
            settings.Entries.Add(entry);
            DoomList.SelectedItem = entry;
        }
    }


    private async Task<bool> ChooseGZDoomPath()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();

        // Need to initialize the picker object with the hwnd / IInitializeWithWindow 
        WinRT.Interop.InitializeWithWindow.Initialize(picker, HWND);

        // Now we can use the picker object as normal
        picker.FileTypeFilter.Add(".exe");
        picker.CommitButtonText = "Выбрать GZDoom";
        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            settings.GZDoomPath = file.Path;
            return true;
        }
        return false;
    }

    public bool IsGZDoomPathChoosen()
    {
        if (string.IsNullOrEmpty(settings.GZDoomPath))
        {
            return false;
        }
        if (!File.Exists(settings.GZDoomPath))
        {
            return false;
        }
        return true;
    }

    private async void RemoveMod_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var entry = button.DataContext as DoomEntry;

        ContentDialog dialog = new()
        {
            XamlRoot = Content.XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            PrimaryButtonText = "Удалить",
            Content = $"Вы уверены, что хотите удалить '{entry.Name}'?",
            CloseButtonText = "Отмена",
            DefaultButton = ContentDialogButton.Primary,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            settings.Entries.Remove(entry);
        }
    }

    public readonly struct EditModDialogResult
    {
        public readonly string name;
        public readonly KeyValue iWadFile;

        public EditModDialogResult(string name, KeyValue iWadFile)
        {
            this.name = name;
            this.iWadFile = iWadFile;
        }
    }

    public async Task<EditModDialogResult?> AddOrEditModDialogShow(string modName, KeyValue iWadFile, bool isEditMode)
    {
        var content = new EditModContentDialog(modName, iWadFile);
        ContentDialog dialog = new()
        {
            XamlRoot = Content.XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            Content = content,
            PrimaryButtonText = isEditMode ? "Изменить" : "Добавить",
            CloseButtonText = "Отмена",
            DefaultButton = ContentDialogButton.Primary,
        };

        if (ContentDialogResult.Primary == await dialog.ShowAsync())
        {
            return new (content.ModName, content.IWadFile);
        }
        return null;
    }
}
