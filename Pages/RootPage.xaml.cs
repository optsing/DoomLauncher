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

    private void SetProgress(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            progressIndicator.Visibility = Visibility.Collapsed;
        }
        else
        {
            progressIndicator.Visibility = Visibility.Visible;
            progressIndicatorText.Text = text;
        }
    }

    private void Page_OnProgress(object? sender, string? e)
    {
        SetProgress(e);
    }

    private async void Page_OnRemove(object? sender, DoomEntry entry)
    {
        await RemoveMod(entry);
    }

    private async void RemoveMod_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el)
        {
            if (el.DataContext is DoomEntry entry)
            {
                await RemoveMod(entry);
            }
        }
    }

    private async void Page_OnEdit(object? sender, DoomEntry entry)
    {
        await EditMod(entry);
    }

    private async void EditMod_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el)
        {
            if (el.DataContext is DoomEntry entry)
            {
                await EditMod(entry);
            }
        }
    }

    private void Page_OnCopy(object? sender, DoomEntry entry)
    {
        CopyMod(entry);
    }

    private void CopyMod_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el)
        {
            if (el.DataContext is DoomEntry entry)
            {
                CopyMod(entry);
            }
        }
    }

    private async void Page_OnExport(object? sender, DoomEntry entry)
    {
        await ExportMod(entry);
    }

    private async void ExportMod_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el)
        {
            if (el.DataContext is DoomEntry entry)
            {
                await ExportMod(entry);
            }
        }
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
            SelectedImageIndex = entry.SelectedImageIndex,
            ModFiles = new(entry.ModFiles.Select(path => path)),
            ImageFiles = new(entry.ImageFiles.Select(path => path)),
        };
        settings.Entries.Add(newEntry);
        DoomList.SelectedItem = newEntry;
    }

    private void Page_OnStart(object? sender, DoomEntry entry)
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
        if (!string.IsNullOrEmpty(entry.IWadFile))
        {
            processInfo.ArgumentList.Add("-iwad");
            processInfo.ArgumentList.Add(entry.IWadFile);
        }
        if (entry.ModFiles.Count > 0)
        {
            processInfo.ArgumentList.Add("-file");
            foreach (var filePath in entry.ModFiles)
            {
                processInfo.ArgumentList.Add(filePath);
            }
        }
        var process = Process.Start(processInfo);
        if (process != null)
        {
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
    }

    private async void Button_Click(object sender, SplitButtonClickEventArgs e)
    {
        if (await AddOrEditModDialogShow(new EditModDialogResult("", "", "", false), false) is EditModDialogResult result)
        {
            var entry = new DoomEntry()
            {
                Id = Guid.NewGuid().ToString(),
                Name = result.name,
                Description = result.description,
                IWadFile = result.iWadFile,
                UniqueConfig = result.uniqueConfig,
                SelectedImageIndex = 0,
            };
            settings.Entries.Add(entry);
            DoomList.SelectedItem = entry;
        }
    }

    private async Task<bool> OpenSettings()
    {
        var dialog = new SettingsContentDialog(XamlRoot, hWnd, new() {
            GZDoomPath = settings.GZDoomPath,
            CloseOnLaunch = settings.CloseOnLaunch,
        });
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
        var filteredIWads = new List<KeyValue>() { new KeyValue("", Settings.IWads[""]) };
        var gzDoomFolderPath = Path.GetDirectoryName(settings.GZDoomPath) ?? "";
        foreach (var iWad in Settings.IWads.Keys)
        {
            if (iWad != "" && File.Exists(Path.Combine(gzDoomFolderPath, iWad)))
            {
                filteredIWads.Add(new KeyValue(iWad, Settings.IWads[iWad]));
            }
        }

        var dialog = new EditModContentDialog(XamlRoot, initial, filteredIWads, isEditMode);
        if (ContentDialogResult.Primary == await dialog.ShowAsync())
        {
            return new(dialog.ModName, dialog.ModDescription, dialog.IWadFile.Key, dialog.UniqueConfig);
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

    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();

        // Need to initialize the picker object with the hwnd / IInitializeWithWindow
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

        // Now we can use the picker object as normal
        picker.FileTypeFilter.Add(".gzdl");

        var files = await picker.PickMultipleFilesAsync();

        if (files.Any())
        {
            await AddEntries(files);
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
        picker.FileTypeChoices.Add("Сборка GZDoomLauncher", new List<string>() { ".gzdl" });
        picker.SuggestedFileName = entry.Name;

        var file = await picker.PickSaveFileAsync();
        if (file == null)
        {
            return;
        }

        SetProgress($"Экспорт: {file.Name}");
        var zipToWrite = await file.OpenStreamForWriteAsync();
        using var archive = new ZipArchive(zipToWrite, ZipArchiveMode.Create);

        var zipConfigEntry = archive.CreateEntry(Path.Combine("entry.json"));
        using (var configStream = zipConfigEntry.Open())
        {
            var fileName = "entry.json";
            SetProgress($"Экспорт: {fileName}");
            var newEntry = new DoomEntry()
            {
                Id = entry.Id,
                Name = entry.Name,
                Description = entry.Description,
                IWadFile = entry.IWadFile,
                UniqueConfig = entry.UniqueConfig,
                SelectedImageIndex = entry.SelectedImageIndex,
                ModFiles = new(entry.ModFiles.Select(path => Path.Combine("mods", Path.GetFileName(path)))),
                ImageFiles = new(entry.ImageFiles.Select(path => Path.Combine("images", Path.GetFileName(path)))),
            };
            await JsonSerializer.SerializeAsync(configStream, newEntry, JsonSettingsContext.Default.DoomEntry);
        }

        foreach (var filePath in entry.ModFiles)
        {
            var fileName = Path.GetFileName(filePath);
            SetProgress($"Экспорт: {fileName}");
            var zipFileEntry = archive.CreateEntry(Path.Combine("mods", fileName));
            using var fileStream = zipFileEntry.Open();
            await File.OpenRead(filePath).CopyToAsync(fileStream);
        }
        foreach (var filePath in entry.ImageFiles)
        {
            var fileName = Path.GetFileName(filePath);
            SetProgress($"Экспорт: {fileName}");
            var zipFileEntry = archive.CreateEntry(Path.Combine("images", fileName));
            using var fileStream = zipFileEntry.Open();
            await File.OpenRead(filePath).CopyToAsync(fileStream);
        }
        SetProgress(null);
    }

    private async Task<DoomEntry?> ImportModFile(StorageFile file)
    {
        SetProgress($"Импорт: {file.Name}");
        using var zipToRead = await file.OpenStreamForReadAsync();
        using var archive = new ZipArchive(zipToRead, ZipArchiveMode.Read);

        if (archive.Entries.FirstOrDefault(entry => entry.FullName == "entry.json") is ZipArchiveEntry zipConfigEntry)
        {
            DoomEntry? entry = null;
            SetProgress($"Импорт: {zipConfigEntry.Name}");
            using var configStream = zipConfigEntry.Open();
            var newEntry = await JsonSerializer.DeserializeAsync(configStream, JsonSettingsContext.Default.DoomEntry);
            if (newEntry != null)
            {
                entry = new DoomEntry()
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = newEntry.Name,
                    Description = newEntry.Description,
                    IWadFile = newEntry.IWadFile,
                    UniqueConfig = newEntry.UniqueConfig,
                    SelectedImageIndex = newEntry.SelectedImageIndex,
                    ModFiles = new(newEntry.ModFiles.Select(path => Path.Combine(dataFolderPath, path))),
                    ImageFiles = new(newEntry.ImageFiles.Select(path => Path.Combine(dataFolderPath, path))),
                };

                foreach (var zipFileEntry in archive.Entries)
                {
                    var zipEntryFolder = Path.GetDirectoryName(zipFileEntry.FullName);
                    if (zipEntryFolder == "mods" || zipEntryFolder == "images")
                    {
                        SetProgress($"Импорт: {zipFileEntry.Name}");
                        using var fileStream = zipFileEntry.Open();
                        await Settings.CopyFileWithConfirmation(XamlRoot, fileStream, zipFileEntry.Name, Path.Combine(dataFolderPath, zipEntryFolder));
                    }
                }

                SetProgress(null);
                return entry;
            }
        }
        await AskDialog.ShowAsync(XamlRoot, "Ошибка импорта", $"Некорректный формат файла '{file.Name}'", "Закрыть", "");
        SetProgress(null);
        return null;
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
                    if (ext == ".gzdl")
                    {
                        e.AcceptedOperation = DataPackageOperation.Copy;
                        break;
                    }
                }
            }
        }
        deferral.Complete();
    }

    private async void DoomList_Drop(object? sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            var entries = new List<StorageFile>();
            foreach (var item in items)
            {
                if (item is StorageFile file)
                {
                    var ext = Path.GetExtension(file.Name).ToLowerInvariant();
                    if (ext == ".gzdl")
                    {
                        entries.Add(file);
                    }
                }
            }
            if (entries.Any())
            {
                await AddEntries(entries);
            }
        }
    }

    private async Task AddEntries(IEnumerable<StorageFile> files)
    {
        DoomEntry? lastEntry = null;
        foreach (var file in files)
        { 
            var entry = await ImportModFile(file);
            if (entry != null)
            {
                settings.Entries.Add(entry);
                lastEntry = entry;
            }
        }
        if (lastEntry != null)
        {
            DoomList.SelectedItem = lastEntry;
        }
    }
}
