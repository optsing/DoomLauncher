using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    public GridLength LeftInset { get; } = new GridLength(0);
    public GridLength RightInset { get; } = new GridLength(0);
    public GridLength TitleBarHeight { get; } = new GridLength(48);

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

            var scaleAdjustment = DPIHelper.GetScaleAdjustment(hWnd);
            if (appWindow.TitleBar.LeftInset > 0)
            {
                LeftInset = new GridLength(appWindow.TitleBar.LeftInset / scaleAdjustment);
            }
            if (appWindow.TitleBar.RightInset > 0)
            {
                RightInset = new GridLength(appWindow.TitleBar.RightInset / scaleAdjustment);
            }
            if (appWindow.TitleBar.Height > 0)
            {
                TitleBarHeight = new GridLength(appWindow.TitleBar.Height / scaleAdjustment);
            }
        }

        frameMain.Content = notSelectedPage;
        DoomList.SelectedIndex = settings.SelectedModIndex;
    }

    public event EventHandler? OnSave;

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
        var scaleAdjustment = XamlRoot.RasterizationScale;

        var dragRect = new Windows.Graphics.RectInt32()
        {
            X = (int)(titleBar.ActualOffset.X * scaleAdjustment),
            Y = (int)(titleBar.ActualOffset.Y * scaleAdjustment),
            Width = (int)(titleBar.ActualWidth * scaleAdjustment),
            Height = (int)(titleBar.ActualHeight * scaleAdjustment),
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
        if (DoomList.SelectedItem is DoomEntry entry)
        {
            settings.SelectedModIndex = DoomList.SelectedIndex;
            DoomPage page = new(entry, hWnd, dataFolderPath);
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
            OnSave?.Invoke(this, new EventArgs());
        }
    }

    private async Task EditMod(DoomEntry entry)
    {
        var initial = new EditModDialogResult(entry);
        if (await AddOrEditModDialogShow(initial, EditDialogMode.Edit) is EditModDialogResult result)
        {
            entry.Name = result.name;
            entry.Description = result.description;
            entry.LongDescription = result.longDescription;
            entry.IWadFile = result.iWadFile;
            entry.UniqueConfig = result.uniqueConfig;
            entry.UniqueSavesFolder = result.uniqueSavesFolder;
            OnSave?.Invoke(this, new EventArgs());
        }
    }

    private void CopyMod(DoomEntry entry)
    {
        var newEntry = new DoomEntry()
        {
            Id = Guid.NewGuid().ToString(),
            Name = entry.Name,
            Description = entry.Description,
            LongDescription = entry.LongDescription,
            IWadFile = entry.IWadFile,
            UniqueConfig = entry.UniqueConfig,
            UniqueSavesFolder = entry.UniqueSavesFolder,
            SelectedImageIndex = entry.SelectedImageIndex,
            ModFiles = new(entry.ModFiles.Select(path => path)),
            ImageFiles = new(entry.ImageFiles.Select(path => path)),
        };
        settings.Entries.Add(newEntry);
        DoomList.SelectedItem = newEntry;
        OnSave?.Invoke(this, new EventArgs());
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
            var entryFolderPath = Path.Combine(dataFolderPath, "entries", entry.Id);
            if (!Directory.Exists(entryFolderPath))
            {
                Directory.CreateDirectory(entryFolderPath);
            }
            var configPath = Path.Combine(entryFolderPath, "config.ini");
            processInfo.ArgumentList.Add("-config");
            processInfo.ArgumentList.Add(configPath);
        }
        if (entry.UniqueSavesFolder)
        {
            var entrySavesFolderPath = Path.Combine(dataFolderPath, "entries", entry.Id, "saves");
            if (!Directory.Exists(entrySavesFolderPath))
            {
                Directory.CreateDirectory(entrySavesFolderPath);
            }
            processInfo.ArgumentList.Add("-savedir");
            processInfo.ArgumentList.Add(entrySavesFolderPath);
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
            WindowHelper.SetForegroundWindow(process.MainWindowHandle);

            if (settings.CloseOnLaunch)
            {
                Application.Current.Exit();
            }
            else if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.Minimize();
                await process.WaitForExitAsync();
                presenter.Restore();
                WindowHelper.SetForegroundWindow(hWnd);
            }
        }
    }

    private async void CreateMod_Click(object sender, SplitButtonClickEventArgs e)
    {
        var initial = new EditModDialogResult(new DoomEntry());
        if (await AddOrEditModDialogShow(initial, EditDialogMode.Create) is EditModDialogResult result)
        {
            var entry = new DoomEntry()
            {
                Id = Guid.NewGuid().ToString(),
                Name = result.name,
                Description = result.description,
                LongDescription = result.longDescription,
                IWadFile = result.iWadFile,
                UniqueConfig = result.uniqueConfig,
                UniqueSavesFolder = result.uniqueSavesFolder,
                SelectedImageIndex = 0,
            };
            settings.Entries.Add(entry);
            DoomList.SelectedItem = entry;
            OnSave?.Invoke(this, new EventArgs());
        }
    }

    private async Task<bool> OpenSettings()
    {
        var dialog = new SettingsContentDialog(XamlRoot, hWnd, new()
        {
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

    public async Task<EditModDialogResult?> AddOrEditModDialogShow(EditModDialogResult initial, EditDialogMode mode)
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

        var dialog = new EditModContentDialog(XamlRoot, initial, filteredIWads, mode);
        if (ContentDialogResult.Primary == await dialog.ShowAsync())
        {
            var modFiles = dialog.ModFiles.Where(tc => tc.IsChecked).Select(tc => tc.Title).ToList();
            var imageFiles = dialog.ImageFiles.Where(tc => tc.IsChecked).Select(tc => tc.Title).ToList();
            return new EditModDialogResult(
                new DoomEntry()
                {
                    Name = dialog.ModName,
                    Description = dialog.ModDescription,
                    LongDescription = dialog.ModLongDescription,
                    IWadFile = dialog.IWadFile.Key,
                    UniqueConfig = dialog.UniqueConfig,
                    UniqueSavesFolder = dialog.UniqueSavesFolder,
                }, modFiles, imageFiles);
        }
        return null;
    }

    private void ButtonMenu_Click(object sender, RoutedEventArgs e)
    {
        swMain.IsPaneOpen = !swMain.IsPaneOpen;
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (await OpenSettings())
        {
            OnSave?.Invoke(this, new EventArgs());
        }
    }

    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();

        // Need to initialize the picker object with the hwnd / IInitializeWithWindow
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

        // Now we can use the picker object as normal
        picker.FileTypeFilter.Add(".gzdl");
        picker.FileTypeFilter.Add(".zip");

        var files = await picker.PickMultipleFilesAsync();

        if (files.Any())
        {
            await ImportEntriesFromFiles(files, withConfirm: true);
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
                LongDescription = entry.LongDescription,
                IWadFile = entry.IWadFile,
                UniqueConfig = entry.UniqueConfig,
                UniqueSavesFolder = entry.UniqueSavesFolder,
                SelectedImageIndex = entry.SelectedImageIndex,
                ModFiles = new(entry.ModFiles.Select(path => Path.GetFileName(path))),
                ImageFiles = new(entry.ImageFiles.Select(path => Path.GetFileName(path))),
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
    public async Task ImportEntriesFromFiles(IReadOnlyList<StorageFile> files, bool withConfirm)
    {
        DoomEntry? lastAddedEntry = null;
        foreach (var file in files)
        {
            var entry = await ImportModFile(file, withConfirm);
            if (entry != null)
            {
                settings.Entries.Add(entry);
                lastAddedEntry = entry;
            }
        }
        if (lastAddedEntry != null)
        {
            DoomList.SelectedItem = lastAddedEntry;
            OnSave?.Invoke(this, new EventArgs());
        }
    }

    public async Task ImportEntryFromDoomWorldId(string wadId, bool withConfirm)
    {
        SetProgress($"Получение информации...");
        var wadInfo = await DoomWorldAPI.GetWADInfo(wadId);
        if (wadInfo != null)
        {
            var entry = await ImportModFileFromDoomWorld(wadInfo, withConfirm);
            if (entry != null)
            {
                settings.Entries.Add(entry);
                DoomList.SelectedItem = entry;
                OnSave?.Invoke(this, new EventArgs());
            }
        }
        SetProgress(null);
    }

    private async Task<DoomEntry?> ImportModFile(StorageFile file, bool withConfirm)
    {
        try
        {
            SetProgress($"Чтение файла: {file.Name}");
            using var zipToRead = await file.OpenStreamForReadAsync();
            using var archive = new ZipArchive(zipToRead, ZipArchiveMode.Read);
            DoomEntry? newEntry = null;
            if (archive.Entries.FirstOrDefault(entry => entry.FullName == "entry.json") is ZipArchiveEntry zipConfigEntry)
            {
                SetProgress($"Извлечение: {zipConfigEntry.Name}");
                using var configStream = zipConfigEntry.Open();
                newEntry = await JsonSerializer.DeserializeAsync(configStream, JsonSettingsContext.Default.DoomEntry);
            }
            newEntry ??= new DoomEntry()
            {
                Name = Path.GetFileNameWithoutExtension(file.Name),
            };
            var entryProperties = new EditModDialogResult(newEntry);
            if (withConfirm)
            {
                foreach (var zipFileEntry in archive.Entries)
                {
                    var ext = Path.GetExtension(zipFileEntry.Name).ToLowerInvariant();
                    if (Settings.SupportedModExtensions.Contains(ext))
                    {
                        entryProperties.modFiles.Add(zipFileEntry.Name);
                    }
                    else if (Settings.SupportedImageExtensions.Contains(ext))
                    {
                        entryProperties.imageFiles.Add(zipFileEntry.Name);
                    }
                }
                entryProperties = await AddOrEditModDialogShow(entryProperties, EditDialogMode.Import);
            }
            if (entryProperties != null)
            {
                var modsCopied = new List<string>();
                var imagesCopied = new List<string>();
                foreach (var zipFileEntry in archive.Entries)
                {
                    var ext = Path.GetExtension(zipFileEntry.Name).ToLowerInvariant();
                    if (Settings.SupportedModExtensions.Contains(ext) && (!withConfirm || entryProperties.modFiles.Contains(zipFileEntry.Name)))
                    {
                        SetProgress($"Извлечение: {zipFileEntry.Name}");
                        using var fileStream = zipFileEntry.Open();
                        await Settings.CopyFileWithConfirmation(XamlRoot, fileStream, zipFileEntry.Name, Path.Combine(dataFolderPath, "mods"));
                        modsCopied.Add(zipFileEntry.Name);
                    }
                    else if (Settings.SupportedImageExtensions.Contains(ext) && (!withConfirm || entryProperties.imageFiles.Contains(zipFileEntry.Name)))
                    {
                        SetProgress($"Извлечение: {zipFileEntry.Name}");
                        using var fileStream = zipFileEntry.Open();
                        await Settings.CopyFileWithConfirmation(XamlRoot, fileStream, zipFileEntry.Name, Path.Combine(dataFolderPath, "images"));
                        imagesCopied.Add(zipFileEntry.Name);
                    }
                }

                var finalModFiles = newEntry.ModFiles
                    .Where(modsCopied.Contains)
                    .Concat(modsCopied
                        .Where(fileName => !newEntry.ModFiles.Contains(fileName)))
                    .Select(fileName => Path.Combine(dataFolderPath, "mods", fileName));
                var finalImageFiles = newEntry.ImageFiles
                    .Where(imagesCopied.Contains)
                    .Concat(imagesCopied
                        .Where(fileName => !newEntry.ImageFiles.Contains(fileName)))
                    .Select(fileName => Path.Combine(dataFolderPath, "images", fileName));

                SetProgress(null);
                return new DoomEntry()
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = entryProperties.name,
                    Description = entryProperties.description,
                    LongDescription = entryProperties.longDescription,
                    IWadFile = entryProperties.iWadFile,
                    UniqueConfig = entryProperties.uniqueConfig,
                    UniqueSavesFolder = entryProperties.uniqueSavesFolder,
                    SelectedImageIndex = newEntry.SelectedImageIndex,
                    ModFiles = new(finalModFiles),
                    ImageFiles = new(finalImageFiles),
                };
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }
        // await AskDialog.ShowAsync(XamlRoot, "Ошибка импорта", $"Некорректный формат файла '{file.Name}'", "Закрыть", "");
        SetProgress(null);
        return null;
    }


    [GeneratedRegex(@"\s*<br>\s*")]
    private static partial Regex reLineBreak();

    private async Task<DoomEntry?> ImportModFileFromDoomWorld(DoomWorldFileEntry wadInfo, bool withConfirm)
    {
        try
        {
            SetProgress($"Чтение файла: {wadInfo.Filename}");
            var zipToRead = await DoomWorldAPI.DownloadWadArchive(wadInfo);
            using var archive = new ZipArchive(zipToRead, ZipArchiveMode.Read);

            var entryProperties = new EditModDialogResult(new DoomEntry()
            {
                Name = wadInfo.Title,
                LongDescription = reLineBreak().Replace(wadInfo.Description, "\n"),
            });
            if (withConfirm)
            {
                foreach (var zipFileEntry in archive.Entries)
                {
                    var ext = Path.GetExtension(zipFileEntry.Name).ToLowerInvariant();
                    if (Settings.SupportedModExtensions.Contains(ext))
                    {
                        entryProperties.modFiles.Add(zipFileEntry.Name);
                    }
                    else if (Settings.SupportedImageExtensions.Contains(ext))
                    {
                        entryProperties.imageFiles.Add(zipFileEntry.Name);
                    }
                }
                entryProperties = await AddOrEditModDialogShow(entryProperties, EditDialogMode.Import);
            }
            if (entryProperties != null)
            {
                var modsCopied = new List<string>();
                var imagesCopied = new List<string>();
                foreach (var zipFileEntry in archive.Entries)
                {
                    var ext = Path.GetExtension(zipFileEntry.Name).ToLowerInvariant();
                    if (Settings.SupportedModExtensions.Contains(ext) && (!withConfirm || entryProperties.modFiles.Contains(zipFileEntry.Name)))
                    {
                        SetProgress($"Извлечение: {zipFileEntry.Name}");
                        using var fileStream = zipFileEntry.Open();
                        await Settings.CopyFileWithConfirmation(XamlRoot, fileStream, zipFileEntry.Name, Path.Combine(dataFolderPath, "mods"));
                        modsCopied.Add(zipFileEntry.Name);
                    }
                    else if (Settings.SupportedImageExtensions.Contains(ext) && (!withConfirm || entryProperties.imageFiles.Contains(zipFileEntry.Name)))
                    {
                        SetProgress($"Извлечение: {zipFileEntry.Name}");
                        using var fileStream = zipFileEntry.Open();
                        await Settings.CopyFileWithConfirmation(XamlRoot, fileStream, zipFileEntry.Name, Path.Combine(dataFolderPath, "images"));
                        imagesCopied.Add(zipFileEntry.Name);
                    }
                }

                var finalModFiles = modsCopied
                    .Select(fileName => Path.Combine(dataFolderPath, "mods", fileName));
                var finalImageFiles = imagesCopied
                    .Select(fileName => Path.Combine(dataFolderPath, "images", fileName));

                SetProgress(null);
                return new DoomEntry()
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = entryProperties.name,
                    Description = entryProperties.description,
                    LongDescription = entryProperties.longDescription,
                    IWadFile = entryProperties.iWadFile,
                    UniqueConfig = entryProperties.uniqueConfig,
                    UniqueSavesFolder = entryProperties.uniqueSavesFolder,
                    SelectedImageIndex = 0,
                    ModFiles = new(finalModFiles),
                    ImageFiles = new(finalImageFiles),
                };
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }
        SetProgress(null);
        return null;
    }

    private async void Root_DragEnter(object sender, DragEventArgs e)
    {
        try
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                foreach (var item in items)
                {
                    if (item is StorageFile file)
                    {
                        var ext = Path.GetExtension(file.Name).ToLowerInvariant();
                        if (ext == ".gzdl" || ext == ".zip")
                        {
                            DropHelper.Visibility = Visibility.Visible;
                            return;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }
    }

    private void Root_DragLeave(object sender, DragEventArgs e)
    {
        DropHelper.Visibility = Visibility.Collapsed;
    }

    private void DoomList_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void DoomList_Drop(object? sender, DragEventArgs e)
    {
        DropHelper.Visibility = Visibility.Collapsed;
        try
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                var files = new List<StorageFile>();
                var mods = new List<StorageFile>();
                var images = new List<StorageFile>();
                foreach (var item in items)
                {
                    if (item is StorageFile file)
                    {
                        var ext = Path.GetExtension(file.Name).ToLowerInvariant();
                        if (ext == ".gzdl" || ext == ".zip")
                        {
                            files.Add(file);
                        } else if (Settings.SupportedModExtensions.Contains(ext))
                        {
                            mods.Add(file);
                        }
                        else if (Settings.SupportedImageExtensions.Contains(ext))
                        {
                            images.Add(file);
                        }
                    }
                }
                if (files.Any())
                {
                    await ImportEntriesFromFiles(files, withConfirm: true);
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }
    }
}
