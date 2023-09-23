using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DoomLauncher;

public enum AnimationDirection
{
    None, Previous, Next
}

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class RootPage : Page
{
    public GridLength LeftInset { get; } = new GridLength(0);
    public GridLength RightInset { get; } = new GridLength(0);
    public GridLength TitleBarHeight { get; } = new GridLength(48);

    private readonly TimeSpan SlideshowAnimationDuration = TimeSpan.FromMilliseconds(150);

    public RootPage(AppWindow appWindow, IntPtr hWnd, string dataFolderPath)
    {
        InitializeComponent();

        this.appWindow = appWindow;
        this.hWnd = hWnd;
        this.dataFolderPath = dataFolderPath;

        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            titleBar.Loaded += TitleBar_Loaded;
            titleBar.SizeChanged += TitleBar_SizeChanged;

            var scaleAdjustment = WinApi.GetScaleAdjustment(hWnd);
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
        DoomList.SelectedIndex = Settings.Current.SelectedModIndex;
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
        if (InputNonClientPointerSource.GetForWindowId(appWindow.Id) is InputNonClientPointerSource NonClientSource)
        {
            var scaleAdjustment = XamlRoot.RasterizationScale;
            var dragRect = new Windows.Graphics.RectInt32()
            {
                X = (int)(titleBar.ActualOffset.X * scaleAdjustment),
                Y = (int)(titleBar.ActualOffset.Y * scaleAdjustment),
                Width = (int)(titleBar.ActualWidth * scaleAdjustment),
                Height = (int)(titleBar.ActualHeight * scaleAdjustment),
            };
            NonClientSource.SetRegionRects(NonClientRegionKind.Caption, new[] { dragRect });
        }
    }

    private readonly IntPtr hWnd;
    private readonly string dataFolderPath;

    private readonly NotSelectedPage notSelectedPage = new();

    private readonly AppWindow appWindow;

    private void DoomList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DoomList.SelectedItem is DoomEntry entry)
        {
            Settings.Current.SelectedModIndex = DoomList.SelectedIndex;
            DoomPage page = new(entry, hWnd, dataFolderPath);
            page.OnStart += Page_OnStart;
            page.OnEdit += Page_OnEdit;
            page.OnCopy += Page_OnCopy;
            page.OnCreateShortcut += Page_OnCreateShortcut;
            page.OnExport += Page_OnExport;
            page.OnRemove += Page_OnRemove;
            page.OnProgress += Page_OnProgress;
            page.OnChangeBackground += Page_OnChangeBackground;
            frameMain.Content = page;
        }
        else
        {
            frameMain.Content = notSelectedPage;
            Page_OnChangeBackground(null, (null, AnimationDirection.None));
        }
        if (swMain.DisplayMode == SplitViewDisplayMode.Overlay)
        {
            swMain.IsPaneOpen = false;
        }
    }

    private readonly SemaphoreSlim semaphoreAnimation = new(1, 1);
    private async void Page_OnChangeBackground(object? sender, (Microsoft.UI.Xaml.Media.Imaging.BitmapImage? bitmap, AnimationDirection direction) e)
    {
        await semaphoreAnimation.WaitAsync();
        bool hasPrevBitmap = imgBackground.Source != null;
        if (hasPrevBitmap)
        {
            if (e.bitmap != null)
            {
                if (e.direction == AnimationDirection.Next)
                {
                    sbToLeft.Begin();
                }
                else if (e.direction == AnimationDirection.Previous)
                {
                    sbToRight.Begin();
                }
            }
            sbHide.Begin();
            await Task.Delay(SlideshowAnimationDuration);
        }
        imgBackground.Source = e.bitmap;
        if (imgBackground.Source != null)
        {
            if (hasPrevBitmap)
            {
                if (e.direction == AnimationDirection.Next)
                {
                    sbFromRight.Begin();
                }
                else if (e.direction == AnimationDirection.Previous)
                {
                    sbFromLeft.Begin();
                }
            }
            sbShow.Begin();
            await Task.Delay(SlideshowAnimationDuration);
        }
        semaphoreAnimation.Release();
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

    private async void Page_OnCreateShortcut(object? sender, DoomEntry entry)
    {
        await CreateShortcut(entry);
    }

    private async void CreateShortcut_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el)
        {
            if (el.DataContext is DoomEntry entry)
            {
                await CreateShortcut(entry);
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
            Settings.Current.Entries.Remove(entry);
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
            entry.GZDoomPath = result.gZDoomPath;
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
            GZDoomPath = entry.GZDoomPath,
            IWadFile = entry.IWadFile,
            UniqueConfig = entry.UniqueConfig,
            UniqueSavesFolder = entry.UniqueSavesFolder,
            SelectedImageIndex = entry.SelectedImageIndex,
            ModFiles = new(entry.ModFiles),
            ImageFiles = new(entry.ImageFiles),
        };
        Settings.Current.Entries.Add(newEntry);
        DoomList.SelectedItem = newEntry;
        OnSave?.Invoke(this, new EventArgs());
    }

    private void Page_OnStart(object? sender, DoomEntry entry)
    {
        LaunchEntry(entry, forceClose: false);
    }

    public void LaunchEntryById(string entryId, bool forceClose)
    {
        var entry = Settings.Current.Entries.FirstOrDefault(entry => entry.Id == entryId);
        if (entry != null)
        {
            LaunchEntry(entry, forceClose);
        }
    }

    public void LaunchEntryByName(string entryName, bool forceClose)
    {
        var entry = Settings.Current.Entries.FirstOrDefault(entry => entry.Name == entryName);
        if (entry != null)
        {
            LaunchEntry(entry, forceClose);
        }
    }

    private async void LaunchEntry(DoomEntry entry, bool forceClose)
    {
        var result = LaunchHelper.LaunchEntry(entry, dataFolderPath);
        if (result == LaunchResult.Success && LaunchHelper.CurrentProcess != null)
        {
            if (Settings.Current.CloseOnLaunch || forceClose)
            {
                Application.Current.Exit();
            }
            else
            {
                if (appWindow.Presenter is OverlappedPresenter minimizePresenter)
                {
                    minimizePresenter.Minimize();
                }
                await LaunchHelper.CurrentProcess.WaitForExitAsync();
                if (appWindow.Presenter is OverlappedPresenter maximizePresenter)
                {
                    maximizePresenter.Restore();
                }
                WinApi.SetForegroundWindow(hWnd);
            }
        }
        else if (result == LaunchResult.AlreadyLaunched && LaunchHelper.CurrentProcess != null)
        {
            if (await AskDialog.ShowAsync(XamlRoot, "Игра уже запущена", "Закройте текущую игру, чтобы запустить новую", "Переключить на игру", "Отмена"))
            {
                WinApi.SetForegroundWindow(LaunchHelper.CurrentProcess.MainWindowHandle);
                if (appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.Minimize();
                }
            }
        }
        else if (result == LaunchResult.PathNotValid)
        {
            if (await AskDialog.ShowAsync(XamlRoot, "Не выбран GZDoom", "Выберите нужную версию GZDoom в настройках сборки", "Перейти в настройки", "Отмена"))
            {
                await EditMod(entry);
            }
        }
        else
        {
            await AskDialog.ShowAsync(XamlRoot, "Ошибка при запуске игры", "Не удалось запустить новую игру", "", "Отмена");
        }
    }

    public async Task<EditModDialogResult?> AddOrEditModDialogShow(EditModDialogResult initial, EditDialogMode mode)
    {
        var dialog = new EditModContentDialog(XamlRoot, initial, mode);
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
                    GZDoomPath = dialog.GZDoomPackage.Path,
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

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (frameMain.Content is not SettingsPage)
        {
            DoomList.SelectedItem = null;
            var settingsPage = new SettingsPage(hWnd, dataFolderPath);
            settingsPage.OnProgress += Page_OnProgress;
            frameMain.Content = settingsPage;
            Page_OnChangeBackground(null, (null, AnimationDirection.None));
        }
    }

    private async void CreateEntryFromFiles_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();

        // Need to initialize the picker object with the hwnd / IInitializeWithWindow
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

        // Now we can use the picker object as normal
        foreach (var ext in FileHelper.SupportedModExtensions)
        {
            picker.FileTypeFilter.Add(ext);
        }
        foreach (var ext in FileHelper.SupportedImageExtensions)
        {
            picker.FileTypeFilter.Add(ext);
        }

        var files = await picker.PickMultipleFilesAsync();

        if (files.Any())
        {
            var mods = files.Where(file => FileHelper.SupportedModExtensions.Contains(Path.GetExtension(file.Name))).ToList();
            var images = files.Where(file => FileHelper.SupportedImageExtensions.Contains(Path.GetExtension(file.Name))).ToList();
            var newEntry = await CreateModFromFiles(mods, images, withConfirm: true);
            if (newEntry != null)
            {
                Settings.Current.Entries.Add(newEntry);
                DoomList.SelectedItem = newEntry;
                OnSave?.Invoke(this, new EventArgs());
            }
        }
    }

    private async void CreateMod_Click(object sender, SplitButtonClickEventArgs e)
    {
        var initial = new EditModDialogResult(new DoomEntry());
        if (await AddOrEditModDialogShow(initial, EditDialogMode.Create) is EditModDialogResult result)
        {
            var newEntry = new DoomEntry()
            {
                Id = Guid.NewGuid().ToString(),
                Name = result.name,
                Description = result.description,
                LongDescription = result.longDescription,
                GZDoomPath = result.gZDoomPath,
                IWadFile = result.iWadFile,
                UniqueConfig = result.uniqueConfig,
                UniqueSavesFolder = result.uniqueSavesFolder,
                SelectedImageIndex = 0,
            };
            Settings.Current.Entries.Add(newEntry);
            DoomList.SelectedItem = newEntry;
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

    private async Task CreateShortcut(DoomEntry entry)
    {
        var picker = new Windows.Storage.Pickers.FileSavePicker();

        // Need to initialize the picker object with the hwnd / IInitializeWithWindow
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

        // Now we can use the picker object as normal
        picker.FileTypeChoices.Add("Ярлык", new List<string>() { ".url" });
        picker.SuggestedFileName = entry.Name;
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
        picker.CommitButtonText = "Создать ярлык";

        var file = await picker.PickSaveFileAsync();
        if (file == null)
        {
            return;
        }

        SetProgress("Создание ярлыка");
        await FileHelper.CreateEntryShortcut(entry, file);
        SetProgress(null);
    }

        private async Task ExportMod(DoomEntry entry)
    {
        var picker = new Windows.Storage.Pickers.FileSavePicker();

        // Need to initialize the picker object with the hwnd / IInitializeWithWindow
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

        // Now we can use the picker object as normal
        picker.FileTypeChoices.Add("Сборка GZDoomLauncher", new List<string>() { ".gzdl" });
        picker.SuggestedFileName = entry.Name;
        picker.CommitButtonText = "Экспортировать";

        var file = await picker.PickSaveFileAsync();
        if (file == null)
        {
            return;
        }

        SetProgress($"Экспорт: {file.Name}");
        using var zipToWrite = await file.OpenStreamForWriteAsync();
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
                GZDoomPath = entry.GZDoomPath,
                IWadFile = entry.IWadFile,
                UniqueConfig = entry.UniqueConfig,
                UniqueSavesFolder = entry.UniqueSavesFolder,
                SelectedImageIndex = entry.SelectedImageIndex,
                ModFiles = new(entry.ModFiles.Select(path => Path.GetFileName(path))),
                ImageFiles = new(entry.ImageFiles.Select(path => Path.GetFileName(path))),
            };
            await JsonSerializer.SerializeAsync(configStream, newEntry, JsonSettingsContext.Default.DoomEntry);
        }

        var modsFolderPath = Path.Combine(dataFolderPath, "mods");
        var imagesFolderPath = Path.Combine(dataFolderPath, "images");
        foreach (var filePath in entry.ModFiles)
        {
            var fullPath = Path.GetFullPath(filePath, modsFolderPath);
            var fileName = Path.GetFileName(filePath);
            SetProgress($"Экспорт: {fileName}");
            var zipFileEntry = archive.CreateEntry(Path.Combine("mods", fileName));
            using var fileStream = zipFileEntry.Open();
            await File.OpenRead(fullPath).CopyToAsync(fileStream);
        }
        foreach (var filePath in entry.ImageFiles)
        {
            var fullPath = Path.GetFullPath(filePath, imagesFolderPath);
            var fileName = Path.GetFileName(filePath);
            SetProgress($"Экспорт: {fileName}");
            var zipFileEntry = archive.CreateEntry(Path.Combine("images", fileName));
            using var fileStream = zipFileEntry.Open();
            await File.OpenRead(fullPath).CopyToAsync(fileStream);
        }
        SetProgress(null);
    }
    public async Task ImportEntriesFromFiles(IReadOnlyList<StorageFile> files, bool withConfirm)
    {
        DoomEntry? lastAddedEntry = null;
        foreach (var file in files)
        {
            var newEntry = await ImportModFile(file, withConfirm);
            if (newEntry != null)
            {
                Settings.Current.Entries.Add(newEntry);
                lastAddedEntry = newEntry;
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
        var wadInfo = await WebAPI.Current.GetDoomWorldWADInfo(wadId);
        if (wadInfo != null)
        {
            var newEntry = await ImportModFileFromDoomWorld(wadInfo, withConfirm);
            if (newEntry != null)
            {
                Settings.Current.Entries.Add(newEntry);
                DoomList.SelectedItem = newEntry;
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
                    if (FileHelper.SupportedModExtensions.Contains(ext))
                    {
                        entryProperties.modFiles.Add(zipFileEntry.Name);
                    }
                    else if (FileHelper.SupportedImageExtensions.Contains(ext))
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
                var modsFolderPath = Path.Combine(dataFolderPath, "mods");
                var imagesFolderPath = Path.Combine(dataFolderPath, "images");
                foreach (var zipFileEntry in archive.Entries)
                {
                    var ext = Path.GetExtension(zipFileEntry.Name).ToLowerInvariant();
                    if (FileHelper.SupportedModExtensions.Contains(ext) && (!withConfirm || entryProperties.modFiles.Contains(zipFileEntry.Name)))
                    {
                        SetProgress($"Извлечение: {zipFileEntry.Name}");
                        using var fileStream = zipFileEntry.Open();
                        await FileHelper.CopyFileWithConfirmation(XamlRoot, fileStream, zipFileEntry.Name, modsFolderPath);
                        modsCopied.Add(zipFileEntry.Name);
                    }
                    else if (FileHelper.SupportedImageExtensions.Contains(ext) && (!withConfirm || entryProperties.imageFiles.Contains(zipFileEntry.Name)))
                    {
                        SetProgress($"Извлечение: {zipFileEntry.Name}");
                        using var fileStream = zipFileEntry.Open();
                        await FileHelper.CopyFileWithConfirmation(XamlRoot, fileStream, zipFileEntry.Name, imagesFolderPath);
                        imagesCopied.Add(zipFileEntry.Name);
                    }
                }

                var finalModFiles = newEntry.ModFiles
                    .Where(modsCopied.Contains)
                    .Concat(modsCopied
                        .Where(fileName => !newEntry.ModFiles.Contains(fileName)));
                var finalImageFiles = newEntry.ImageFiles
                    .Where(imagesCopied.Contains)
                    .Concat(imagesCopied
                        .Where(fileName => !newEntry.ImageFiles.Contains(fileName)));

                SetProgress(null);
                return new DoomEntry()
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = entryProperties.name,
                    Description = entryProperties.description,
                    LongDescription = entryProperties.longDescription,
                    GZDoomPath = entryProperties.gZDoomPath,
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
            using var zipToRead = await WebAPI.Current.DownloadDoomWorldWadArchive(wadInfo);
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
                    if (FileHelper.SupportedModExtensions.Contains(ext))
                    {
                        entryProperties.modFiles.Add(zipFileEntry.Name);
                    }
                    else if (FileHelper.SupportedImageExtensions.Contains(ext))
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
                var modsFolderPath = Path.Combine(dataFolderPath, "mods");
                var imagesFolderPath = Path.Combine(dataFolderPath, "images");
                foreach (var zipFileEntry in archive.Entries)
                {
                    var ext = Path.GetExtension(zipFileEntry.Name).ToLowerInvariant();
                    if (FileHelper.SupportedModExtensions.Contains(ext) && (!withConfirm || entryProperties.modFiles.Contains(zipFileEntry.Name)))
                    {
                        SetProgress($"Извлечение: {zipFileEntry.Name}");
                        using var fileStream = zipFileEntry.Open();
                        await FileHelper.CopyFileWithConfirmation(XamlRoot, fileStream, zipFileEntry.Name, modsFolderPath);
                        modsCopied.Add(zipFileEntry.Name);
                    }
                    else if (FileHelper.SupportedImageExtensions.Contains(ext) && (!withConfirm || entryProperties.imageFiles.Contains(zipFileEntry.Name)))
                    {
                        SetProgress($"Извлечение: {zipFileEntry.Name}");
                        using var fileStream = zipFileEntry.Open();
                        await FileHelper.CopyFileWithConfirmation(XamlRoot, fileStream, zipFileEntry.Name, imagesFolderPath);
                        imagesCopied.Add(zipFileEntry.Name);
                    }
                }

                SetProgress(null);
                return new DoomEntry()
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = entryProperties.name,
                    Description = entryProperties.description,
                    LongDescription = entryProperties.longDescription,
                    GZDoomPath = entryProperties.gZDoomPath,
                    IWadFile = entryProperties.iWadFile,
                    UniqueConfig = entryProperties.uniqueConfig,
                    UniqueSavesFolder = entryProperties.uniqueSavesFolder,
                    SelectedImageIndex = 0,
                    ModFiles = new(modsCopied),
                    ImageFiles = new(imagesCopied),
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

    private async Task<DoomEntry?> CreateModFromFiles(List<StorageFile> mods, List<StorageFile> images, bool withConfirm)
    {
        try
        {
            string title = "Новая сборка";
            if (mods.Any())
            {
                title = Path.GetFileNameWithoutExtension(mods.First().Name);
            }
            else if (images.Any())
            {
                title = Path.GetFileNameWithoutExtension(images.First().Name);
            }

            var entryProperties = new EditModDialogResult(new DoomEntry()
            {
                Name = title,
            });
            if (withConfirm)
            {
                foreach (var mod in mods)
                {
                    entryProperties.modFiles.Add(mod.Name);
                }
                foreach (var image in images)
                {
                    entryProperties.imageFiles.Add(image.Name);
                }
                entryProperties = await AddOrEditModDialogShow(entryProperties, EditDialogMode.Create);
            }
            if (entryProperties != null)
            {
                var modsCopied = new List<string>();
                var imagesCopied = new List<string>();
                var modsFolderPath = Path.Combine(dataFolderPath, "mods");
                var imagesFolderPath = Path.Combine(dataFolderPath, "images");
                foreach (var mod in mods)
                {
                    if (!withConfirm || entryProperties.modFiles.Contains(mod.Name))
                    {
                        SetProgress($"Копирование: {mod.Name}");
                        await FileHelper.CopyFileWithConfirmation(XamlRoot, mod, modsFolderPath);
                        modsCopied.Add(mod.Name);
                    }
                }
                foreach (var image in images)
                {
                    if (!withConfirm || entryProperties.imageFiles.Contains(image.Name))
                    {
                        SetProgress($"Копирование: {image.Name}");
                        await FileHelper.CopyFileWithConfirmation(XamlRoot, image, imagesFolderPath);
                        imagesCopied.Add(image.Name);
                    }
                }

                SetProgress(null);
                return new DoomEntry()
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = entryProperties.name,
                    Description = entryProperties.description,
                    LongDescription = entryProperties.longDescription,
                    GZDoomPath = entryProperties.gZDoomPath,
                    IWadFile = entryProperties.iWadFile,
                    UniqueConfig = entryProperties.uniqueConfig,
                    UniqueSavesFolder = entryProperties.uniqueSavesFolder,
                    SelectedImageIndex = 0,
                    ModFiles = new(modsCopied),
                    ImageFiles = new(imagesCopied),
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
                        if (ext == ".gzdl" || ext == ".zip" || FileHelper.SupportedModExtensions.Contains(ext) || FileHelper.SupportedImageExtensions.Contains(ext))
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

    private void DropHelper_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void DropHelper_Drop(object? sender, DragEventArgs e)
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
                        }
                        else if (FileHelper.SupportedModExtensions.Contains(ext))
                        {
                            mods.Add(file);
                        }
                        else if (FileHelper.SupportedImageExtensions.Contains(ext))
                        {
                            images.Add(file);
                        }
                    }
                }
                if (files.Any())
                {
                    await ImportEntriesFromFiles(files, withConfirm: true);
                }
                if (mods.Any() || images.Any())
                {
                    var newEntry = await CreateModFromFiles(mods, images, withConfirm: true);
                    if (newEntry != null)
                    {
                        Settings.Current.Entries.Add(newEntry);
                        DoomList.SelectedItem = newEntry;
                        OnSave?.Invoke(this, new EventArgs());
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }
    }
}
