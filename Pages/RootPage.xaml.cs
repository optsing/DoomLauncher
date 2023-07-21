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
            await OpenSettings(true);
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
            DoomPage page = new(item, hWnd, dataFolderPath, settings);
            page.OnStart += Page_OnStart;
            page.OnEdit += Page_OnEdit;
            page.OnRemove += Page_OnRemove;
            page.OnProgress += Page_OnProgress;
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

    private void Page_OnProgress(object sender, bool e)
    {
        progressIndicator.IsLoading = e;
    }

    private async void Page_OnRemove(object sender, DoomEntry entry)
    {
        await RemoveMod(entry);
    }

    private async void RemoveMod_Click(object sender, RoutedEventArgs e)
    {
        var el = sender as FrameworkElement;
        var entry = el.DataContext as DoomEntry;
        await RemoveMod(entry);
    }

    private async void Page_OnEdit(object sender, DoomEntry entry)
    {
        await EditMod(entry);
    }

    private async void EditMod_Click(object sender, RoutedEventArgs e)
    {
        var el = sender as FrameworkElement;
        var entry = el.DataContext as DoomEntry;
        await EditMod(entry);
    }

    private async Task RemoveMod(DoomEntry entry)
    {
        var dialog = new AskDialog(XamlRoot, "Удаление сборки", $"Вы уверены, что хотите удалить сборку '{entry.Name}'?", "Удалить", "Отмена");
        if (ContentDialogResult.Primary == await dialog.ShowAsync())
        {
            settings.Entries.Remove(entry);
        }
    }

    private async Task EditMod(DoomEntry entry)
    {
        if (await AddOrEditModDialogShow(new EditModDialogResult(entry.Name, entry.Description, entry.IWadFile, entry.UniqueConfig), true) is EditModDialogResult result)
        {
            entry.Name = result.name;
            entry.Description = result.description;
            entry.IWadFile = result.iWadFile;
            entry.UniqueConfig = result.uniqueConfig;
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
            await OpenSettings(true);
            return;
        }
        ProcessStartInfo processInfo = new()
        {
            FileName = settings.GZDoomPath,
            WorkingDirectory = Path.GetDirectoryName(settings.GZDoomPath),
        };
        if (entry.UniqueConfig)
        {
            var configFolderPath = Path.Combine(dataFolderPath, "configs");
            if (!Directory.Exists(configFolderPath))
            {
                Directory.CreateDirectory(configFolderPath);
            }
            var configPath = Path.Combine(configFolderPath, $"{entry.Id}.ini");
            processInfo.ArgumentList.Add("-config");
            processInfo.ArgumentList.Add(configPath);
        }
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
            await process.WaitForExitAsync();
            presenter.Restore();
            SetForegroundWindow(hWnd);
        }
    }

    private async void Button_Click(object sender, RoutedEventArgs e)
    {
        if (await AddOrEditModDialogShow(new EditModDialogResult("", "", Settings.IWads.First(), false), false) is EditModDialogResult result)
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


    private async Task OpenSettings(bool forceGZDoomPathSetup)
    {
        var dialog = new SettingsContentDialog(XamlRoot, hWnd, new() {
            GZDoomPath = settings.GZDoomPath,
            CloseOnLaunch = settings.CloseOnLaunch,
        }, forceGZDoomPathSetup);
        if (ContentDialogResult.Primary == await dialog.ShowAsync())
        {
            settings.GZDoomPath = dialog.State.GZDoomPath;
            settings.CloseOnLaunch = dialog.State.CloseOnLaunch;
        }
    }

    public async Task<EditModDialogResult?> AddOrEditModDialogShow(EditModDialogResult initial, bool isEditMode)
    {
        if (!Settings.ValidateGZDoomPath(settings.GZDoomPath))
        {
            await OpenSettings(true);
            return null;
        }
        List<KeyValue> filteredIWads = new() { Settings.IWads.First() };
        var gzDoomFolderPath = Path.GetDirectoryName(settings.GZDoomPath);
        foreach (var iwad in Settings.IWads)
        {
            if (iwad.Key != "" && File.Exists(Path.Combine(gzDoomFolderPath, iwad.Key)))
            {
                filteredIWads.Add(iwad);
            }
        }

        var dialog = new EditModContentDialog(XamlRoot, initial, filteredIWads, isEditMode);
        if (ContentDialogResult.Primary == await dialog.ShowAsync())
        {
            return new(dialog.ModName, dialog.ModDescription, dialog.IWadFile, dialog.UniqueConfig);
        }
        return null;
    }

    private void ButtonMenu_Click(object sender, RoutedEventArgs e)
    {
        swMain.IsPaneOpen = !swMain.IsPaneOpen;
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        bool forceGZDoomPathSetup = !Settings.ValidateGZDoomPath(settings.GZDoomPath);
        await OpenSettings(forceGZDoomPathSetup);
    }

    public static Visibility HasText(string text)
    {
        return string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }
}
