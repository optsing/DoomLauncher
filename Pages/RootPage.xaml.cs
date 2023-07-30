using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

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
            page.OnCopy += Page_OnCopy;
            page.OnExport += Page_OnExport;
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
        progressIndicator.Visibility = e ? Visibility.Visible : Visibility.Collapsed;
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

    private void Page_OnCopy(object sender, DoomEntry entry)
    {
        CopyMod(entry);
    }

    private void CopyMod_Click(object sender, RoutedEventArgs e)
    {
        var el = sender as FrameworkElement;
        var entry = el.DataContext as DoomEntry;
        CopyMod(entry);
    }

    private async void Page_OnExport(object sender, DoomEntry entry)
    {
        await ExportMod(entry);
    }

    private async void ExportMod_Click(object sender, RoutedEventArgs e)
    {
        var el = sender as FrameworkElement;
        var entry = el.DataContext as DoomEntry;
        await ExportMod(entry);
    }

    private async Task RemoveMod(DoomEntry entry)
    {
        if (await AskDialog.ShowAsync(XamlRoot, "Удаление сборки", $"Вы уверены, что хотите удалить сборку '{entry.Name}'?", "Удалить", "Отмена"))
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

    private void CopyMod(DoomEntry entry)
    {
        var newEntry = new DoomEntry()
        {
            Id = Guid.NewGuid().ToString(),
            Name = entry.Name,
            Description = entry.Description,
            IWadFile = entry.IWadFile,
            UniqueConfig = entry.UniqueConfig,
            ModFiles = new(entry.ModFiles.Select(file => new NamePath(file.Path))),
            ImageFiles = new(entry.ImageFiles.Select(path => path)),
        };
        settings.Entries.Add(newEntry);
        DoomList.SelectedItem = newEntry;
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
            var entry = new DoomEntry()
            {
                Id = Guid.NewGuid().ToString(),
                Name = result.name,
                Description = result.description,
                IWadFile = result.iWadFile,
                UniqueConfig = result.uniqueConfig,
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

    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();

        // Need to initialize the picker object with the hwnd / IInitializeWithWindow
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

        // Now we can use the picker object as normal
        picker.FileTypeFilter.Add(".zip");

        var file = await picker.PickSingleFileAsync();

        if (file == null)
        {
            return;
        }

        var entry = await ImportModFile(file);

        if (entry != null)
        {
            settings.Entries.Add(entry);
            DoomList.SelectedItem = entry;
        }
    }


    public static Visibility HasText(string text)
    {
        return string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    private async Task ExportMod(DoomEntry entry)
    {
        var picker = new Windows.Storage.Pickers.FileSavePicker();

        // Need to initialize the picker object with the hwnd / IInitializeWithWindow
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

        // Now we can use the picker object as normal
        picker.FileTypeChoices.Add("Zip archive", new List<string>() { ".zip" });
        picker.SuggestedFileName = entry.Name;

        var file = await picker.PickSaveFileAsync();

        if (file == null)
        {
            return;
        }

        progressIndicator.Visibility = Visibility.Visible;
        var zipToCreate = await file.OpenStreamForWriteAsync();
        using var archive = new ZipArchive(zipToCreate, ZipArchiveMode.Create);

        foreach (var modFile in entry.ModFiles)
        {
            var filePath = modFile.Path;
            var zipEntry = archive.CreateEntry(Path.Combine("mods", Path.GetFileName(filePath)));
            using var stream = zipEntry.Open();
            await File.OpenRead(filePath).CopyToAsync(stream);
        }
        if (entry.ImageFiles.Any())
        {
            var filePath = entry.ImageFiles.First();
            var zipEntry = archive.CreateEntry(Path.Combine("images", Path.GetFileName(filePath)));
            using var stream = zipEntry.Open();
            await File.OpenRead(filePath).CopyToAsync(stream);
        }
        {
            var newEntry = new DoomEntry()
            {
                Id = entry.Id,
                Name = entry.Name,
                Description = entry.Description,
                UniqueConfig = entry.UniqueConfig,
                IWadFile = entry.IWadFile,
                ModFiles = new(entry.ModFiles.Select(mod => new NamePath(Path.Combine("mods", Path.GetFileName(mod.Path))))),
                ImageFiles = new(entry.ImageFiles.Select(path => Path.Combine("images", Path.GetFileName(path)))),
            };
            var zipEntry = archive.CreateEntry(Path.Combine("entry.json"));
            using var stream = zipEntry.Open();
            await JsonSerializer.SerializeAsync(stream, newEntry, Settings.jsonOptions);
        }
        progressIndicator.Visibility = Visibility.Collapsed;
    }

    private async Task<DoomEntry> ImportModFile(StorageFile file)
    {
        progressIndicator.Visibility = Visibility.Visible;
        using var zipToRead = await file.OpenStreamForReadAsync();
        using var archive = new ZipArchive(zipToRead, ZipArchiveMode.Read);

        if (archive.Entries.FirstOrDefault(entry => entry.FullName == "entry.json") is ZipArchiveEntry zipEntry)
        {
            using var stream = zipEntry.Open();
            var newEntry = await JsonSerializer.DeserializeAsync<DoomEntry>(stream, Settings.jsonOptions);
            var entry = new DoomEntry()
            {
                Id = newEntry.Id,
                Name = newEntry.Name,
                Description = newEntry.Description,
                UniqueConfig = newEntry.UniqueConfig,
                IWadFile = newEntry.IWadFile,
                ModFiles = new(newEntry.ModFiles.Select(mod => new NamePath(Path.Combine(dataFolderPath, mod.Path)))),
                ImageFiles = new(newEntry.ImageFiles.Select(path => Path.Combine(dataFolderPath, path))),
            };

            foreach (var zipFileEntry in archive.Entries)
            {
                var zipEntryFolder = Path.GetDirectoryName(zipFileEntry.FullName);
                if (zipEntryFolder == "mods" || zipEntryFolder == "images")
                {
                    using var fileStream = zipFileEntry.Open();
                    await Settings.CopyFileWithConfirmation(XamlRoot, fileStream, zipFileEntry.Name, Path.Combine(dataFolderPath, zipEntryFolder));
                }
            }

            progressIndicator.Visibility = Visibility.Collapsed;
            return entry;
        }
        else
        {
            await AskDialog.ShowAsync(XamlRoot, "Ошибка импорта", $"Некорректный формат файла '{file.Name}'", "Закрыть", "");
            progressIndicator.Visibility = Visibility.Collapsed;
            return null;
        }
    }

    private async void DoomList_DragOver(object sender, DragEventArgs e)
    {
        var deferral = e.GetDeferral();
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            foreach (var item in items)
            {
                if (item is StorageFile file)
                {
                    var ext = Path.GetExtension(file.Name).ToLowerInvariant();
                    if (ext == ".zip")
                    {
                        e.AcceptedOperation = DataPackageOperation.Copy;
                    }
                }
            }
        }
        deferral.Complete();
    }

    private async void DoomList_Drop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            DoomEntry lastEntry = null;
            foreach (var item in items)
            {
                if (item is StorageFile file)
                {
                    var ext = Path.GetExtension(file.Name).ToLowerInvariant();
                    if (ext == ".zip")
                    {
                        var entry = await ImportModFile(file);
                        if (entry != null)
                        {
                            settings.Entries.Add(entry);
                            lastEntry = entry;
                        }
                    }
                }
            }
            if (lastEntry != null)
            {
                DoomList.SelectedItem = lastEntry;
            }
        }
    }
}
