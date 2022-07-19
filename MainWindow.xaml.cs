using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DoomLauncher;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window, INotifyPropertyChanged
{
    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(titleBar);

        HWND = WinRT.Interop.WindowNative.GetWindowHandle(this);

        try
        {
            // Packaged only
            configFilePath = Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "config.json");
        }
        catch
        {
            // Unpackaged only
            configFilePath = "config.json";
        }

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
            };
        }

        settings.Entries.CollectionChanged += Entries_CollectionChanged;

        DoomList.SelectedIndex = 0;
    }

    private void Entries_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        Save();
    }

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
    private bool hasNoContent = true;

    public event PropertyChangedEventHandler PropertyChanged;

    private void DoomList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DoomList.SelectedItem is DoomEntry item)
        {
            DoomPage page = new(item, HWND);
            page.OnStart += Page_OnStart;
            page.OnSave += Page_OnSave;
            mainFrame.Content = page;
            HasNoContent = false;
        }
        else
        {
            mainFrame.Content = null;
            HasNoContent = true;
        }
    }

    private void Page_OnSave(object sender, EventArgs e)
    {
        Save();
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
            Save();
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
    }

    private void Save()
    {
        var text = JsonSerializer.Serialize<Settings>(settings, jsonOptions);
        File.WriteAllText(configFilePath, text);
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        settings.Entries.Add(new()
        {
            Name = "Новый мод",
            IWadFile = Settings.IWads.First(),
            ModFiles = new(),
        });
        DoomList.SelectedItem = settings.Entries.Last();
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        var ind = DoomList.SelectedIndex;
        settings.Entries.RemoveAt(ind);
        if (settings.Entries.Count > 0)
        {
            DoomList.SelectedIndex = Math.Min(ind, settings.Entries.Count - 1);
        }
    }


    private async Task<bool> ChooseGZDoomPath()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();

        // Need to initialize the picker object with the hwnd / IInitializeWithWindow 
        WinRT.Interop.InitializeWithWindow.Initialize(picker, HWND);

        // Now we can use the picker object as normal
        picker.FileTypeFilter.Add(".exe");
        picker.CommitButtonText = "Выбрать gzdoom.exe";
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

    public bool HasNoContent
    {
        get => hasNoContent;
        set {
            if (hasNoContent != value)
            {
                hasNoContent = value;
                OnPropertyChanged(nameof(HasNoContent));
            }
        }
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
