using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DoomLauncher;
/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class RootPage : Page
{
    [DllImport("User32.dll")]
    private static extern bool SetForegroundWindow(IntPtr handle);

    public RootPage(AppWindow appWindow, Settings settings, IntPtr hWnd, string dataFolderPath)
    {
        InitializeComponent();

        this.appWindow = appWindow;
        this.settings = settings;
        this.hWnd = hWnd;
        this.dataFolderPath = dataFolderPath;

        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            titleBar.Loaded += TitleBar_Loaded;
            titleBar.SizeChanged += TitleBar_SizeChanged;
        }

        frameMain.Content = notSelectedPage;
        DoomList.SelectedIndex = settings.SelectedModIndex;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (!Settings.ValidateGZDoomPath(settings.GZDoomPath))
        {
            await OpenSettings();
        }
    }

    private void TitleBar_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        SetDragRegion();
    }

    private void TitleBar_Loaded(object sender, RoutedEventArgs e)
    {
        SetDragRegion();
    }

    private void SetDragRegion()
    {
        var scaleAdjustment = DPIHelper.GetScaleAdjustment(hWnd);

        var x = (int)(48 * scaleAdjustment);

        var dragRect = new Windows.Graphics.RectInt32()
        {
            X = x,
            Y = 0,
            Width = (int)(titleBar.ActualWidth * scaleAdjustment) - x,
            Height = 48,
        };

        appWindow.TitleBar.SetDragRectangles(new[] { dragRect });
    }

    private readonly IntPtr hWnd;
    private readonly string dataFolderPath;


    private readonly Settings settings;

    private readonly NotSelectedPage notSelectedPage = new();

    private readonly AppWindow appWindow;

    private void DoomList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DoomList.SelectedItem is DoomEntry item)
        {
            settings.SelectedModIndex = DoomList.SelectedIndex;
            DoomPage page = new(item, hWnd, dataFolderPath);
            page.OnStart += Page_OnStart;
            page.OnEdit += Page_OnEdit;
            page.OnRemove += Page_OnRemove;
            frameMain.Content = page;
        }
        else
        {
            frameMain.Content = notSelectedPage;
        }
        if (swMain.DisplayMode == SplitViewDisplayMode.Overlay)
        {
            swMain.IsPaneOpen = false;
        }
    }

    private async void Page_OnRemove(object sender, DoomEntry entry)
    {
        var dialog = new AskDialog(XamlRoot, $"Вы уверены, что хотите удалить сборку '{entry.Name}'?", "Удалить");
        if (ContentDialogResult.Primary == await dialog.ShowAsync())
        {
            settings.Entries.Remove(entry);
        }
    }

    private async void Page_OnEdit(object sender, DoomEntry entry)
    {
        if (await AddOrEditModDialogShow(new EditModDialogResult(entry.Name, entry.IWadFile), true) is EditModDialogResult result)
        {
            entry.Name = result.name;
            entry.IWadFile = result.iWadFile;
        }
    }

    private void Page_OnStart(object sender, DoomEntry entry)
    {
        Start(entry);
    }

    private async void Start(DoomEntry entry)
    {
        if (!Settings.ValidateGZDoomPath(settings.GZDoomPath))
        {
            var success = await OpenSettings();
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
        var process = Process.Start(processInfo);

        SetForegroundWindow(process.MainWindowHandle);

        if (settings.CloseOnLaunch)
        {
            Application.Current.Exit();
        }
        else if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Minimize();
        }
    }

    private async void Button_Click(object sender, RoutedEventArgs e)
    {
        if (await AddOrEditModDialogShow(new EditModDialogResult("", Settings.IWads.First()), false) is EditModDialogResult result)
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


    private async Task<bool> OpenSettings()
    {
        var dialog = new SettingsContentDialog(XamlRoot, hWnd, new() { GZDoomPath = settings.GZDoomPath, CloseOnLaunch = settings.CloseOnLaunch });
        if (ContentDialogResult.Primary == await dialog.ShowAsync())
        {
            settings.GZDoomPath = dialog.State.GZDoomPath;
            settings.CloseOnLaunch = dialog.State.CloseOnLaunch;
            return true;
        }
        return false;
    }

    public async Task<EditModDialogResult?> AddOrEditModDialogShow(EditModDialogResult initial, bool isEditMode)
    {
        if (!Settings.ValidateGZDoomPath(settings.GZDoomPath))
        {
            var success = await OpenSettings();
            if (!success)
            {
                return null;
            }
        }
        List<KeyValue> filteredIWads = new() { Settings.IWads.First() };
        var gzDoomDirectoryPath = Path.GetDirectoryName(settings.GZDoomPath);
        foreach (var iwad in Settings.IWads)
        {
            if (iwad.Key != "" && File.Exists(Path.Combine(gzDoomDirectoryPath, iwad.Key)))
            {
                filteredIWads.Add(iwad);
            }
        }

        var dialog = new EditModContentDialog(XamlRoot, initial, filteredIWads, isEditMode);
        if (ContentDialogResult.Primary == await dialog.ShowAsync())
        {
            return new(dialog.ModName, dialog.IWadFile);
        }
        return null;
    }

    private void ButtonMenu_Click(object sender, RoutedEventArgs e)
    {
        swMain.IsPaneOpen = !swMain.IsPaneOpen;
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        await OpenSettings();
    }

    public static string GZDoomPathTitle(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return "GZDoom не выбран";
        } else
        {
            return path;
        }
    }
}
