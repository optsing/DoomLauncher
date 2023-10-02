using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    public RootPage(AppWindow appWindow)
    {
        InitializeComponent();

        this.appWindow = appWindow;

        if (Settings.IsCustomTitlebar)
        {
            titleBar.Loaded += TitleBar_Loaded;
            titleBar.SizeChanged += TitleBar_SizeChanged;

            var scaleAdjustment = WinApi.GetScaleAdjustment(WinApi.HWND);
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

    private readonly NotSelectedPage notSelectedPage = new();

    private readonly AppWindow appWindow;

    private void DoomList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DoomList.SelectedItem is DoomEntry entry)
        {
            Settings.Current.SelectedModIndex = DoomList.SelectedIndex;
            var page = new DoomPage(entry);
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
    private async void Page_OnChangeBackground(object? sender, (string? imagePath, AnimationDirection direction) e)
    {
        await semaphoreAnimation.WaitAsync();
        var bitmap = string.IsNullOrEmpty(e.imagePath) ? null : await BitmapHelper.CreateBitmapFromFile(e.imagePath);
        bool hasPrevBitmap = imgBackground.Source != null;
        if (hasPrevBitmap)
        {
            if (bitmap != null)
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
        imgBackground.Source = bitmap;
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
        await RemoveEntry(entry);
    }

    private async void RemoveMod_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el)
        {
            if (el.DataContext is DoomEntry entry)
            {
                await RemoveEntry(entry);
            }
        }
    }

    private async void Page_OnEdit(object? sender, DoomEntry entry)
    {
        await EditEntry(entry);
    }

    private async void EditMod_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el)
        {
            if (el.DataContext is DoomEntry entry)
            {
                await EditEntry(entry);
            }
        }
    }

    private void Page_OnCopy(object? sender, DoomEntry entry)
    {
        CopyEntry(entry);
    }

    private void CopyMod_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el)
        {
            if (el.DataContext is DoomEntry entry)
            {
                CopyEntry(entry);
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
        await ExportEntry(entry);
    }

    private async void ExportMod_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el)
        {
            if (el.DataContext is DoomEntry entry)
            {
                await ExportEntry(entry);
            }
        }
    }

    private async Task RemoveEntry(DoomEntry entry)
    {
        if (await AskDialog.ShowAsync(XamlRoot, "Удаление сборки", $"Вы уверены, что хотите удалить сборку '{entry.Name}'?", "Удалить", "Отмена"))
        {
            Settings.Current.Entries.Remove(entry);
            Settings.Save();
            await JumpListHelper.Update();
        }
    }

    private async Task EditEntry(DoomEntry entry)
    {
        var initial = new EditEntryDialogResult(entry);
        if (await EditEntryDialog.ShowAsync(XamlRoot, initial, EditDialogMode.Edit) is EditEntryDialogResult result)
        {
            entry.Name = result.name;
            entry.Description = result.description;
            entry.LongDescription = result.longDescription;
            entry.GZDoomPath = result.gZDoomPath;
            entry.IWadFile = result.iWadFile;
            entry.SteamGame = result.steamGame;
            entry.UniqueConfig = result.uniqueConfig;
            entry.UniqueSavesFolder = result.uniqueSavesFolder;
            Settings.Save();
            await JumpListHelper.Update();
        }
    }

    private void CopyEntry(DoomEntry entry)
    {
        var newEntry = new DoomEntry()
        {
            Id = Guid.NewGuid().ToString(),
            Name = entry.Name,
            Description = entry.Description,
            LongDescription = entry.LongDescription,
            GZDoomPath = entry.GZDoomPath,
            IWadFile = entry.IWadFile,
            SteamGame = entry.SteamGame,
            UniqueConfig = entry.UniqueConfig,
            UniqueSavesFolder = entry.UniqueSavesFolder,
            SelectedImageIndex = entry.SelectedImageIndex,
            ModFiles = new(entry.ModFiles),
            ImageFiles = new(entry.ImageFiles),
        };
        Settings.Current.Entries.Add(newEntry);
        DoomList.SelectedItem = newEntry;
        Settings.Save();
    }

    private void Page_OnStart(object? sender, DoomEntry entry)
    {
        LaunchEntry(entry, forceClose: false);
    }

    public void LaunchEntryById(string entryId, bool forceClose)
    {
        var entry = Settings.Current.Entries.FirstOrDefault(entry => string.Equals(entry.Id, entryId));
        LaunchEntry(entry, forceClose);
    }

    public void LaunchEntryByName(string entryName, bool forceClose)
    {
        var entry = Settings.Current.Entries.FirstOrDefault(entry => string.Equals(entry.Name, entryName));
        LaunchEntry(entry, forceClose);
    }

    private async void LaunchEntry(DoomEntry? entry, bool forceClose)
    {
        if (entry == null)
        {
            await AskDialog.ShowAsync(XamlRoot, "Ошибка при запуске", "Не удалось найти нужную сборку", "", "Отмена");
            return;
        }
        DoomList.SelectedItem = entry;
        var result = LaunchHelper.LaunchEntry(entry);
        if (result == LaunchResult.Success && LaunchHelper.CurrentProcess != null)
        {
            entry.LastLaunch = DateTime.Now;
            await JumpListHelper.Update();
            if (Settings.Current.CloseOnLaunch || forceClose)
            {
                Application.Current.Exit();
            }
            else
            {
                MinimizeAndSwitchToAnotherWindow(LaunchHelper.CurrentProcess.MainWindowHandle);
                await LaunchHelper.CurrentProcess.WaitForExitAsync();
                RestoreAndSwitchToThisWindow();
                if (LaunchHelper.CurrentProcess.ExitCode != 0)
                {
                    await AskDialog.ShowAsync(XamlRoot, "Ошибка при запуске", $"Игра завершилась с ошибкой, код ошибки - {LaunchHelper.CurrentProcess.ExitCode}", "", "Отмена");
                }
            }
        }
        else if (result == LaunchResult.AlreadyLaunched && LaunchHelper.CurrentProcess != null)
        {
            if (await AskDialog.ShowAsync(XamlRoot, "Ошибка при запуске", "Игра уже запущена, закройте текущую игру", "Переключить на игру", "Отмена"))
            {
                MinimizeAndSwitchToAnotherWindow(LaunchHelper.CurrentProcess.MainWindowHandle);
            }
        }
        else if (result == LaunchResult.PathNotValid)
        {
            if (await AskDialog.ShowAsync(XamlRoot, "Ошибка при запуске", "Не выбрана версия GZDoom, выберите нужную версию в настройках сборки", "Перейти в настройки", "Отмена"))
            {
                await EditEntry(entry);
            }
        }
        else
        {
            await AskDialog.ShowAsync(XamlRoot, "Ошибка при запуске", "Не удалось запустить игру", "", "Отмена");
        }
    }

    public void MinimizeAndSwitchToAnotherWindow(IntPtr anotherHWnd)
    {
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Minimize();
        }
        WinApi.SetForegroundWindow(anotherHWnd);
    }

    public void RestoreAndSwitchToThisWindow()
    {
        if (appWindow.Presenter is OverlappedPresenter presenter && presenter.State == OverlappedPresenterState.Minimized)
        {
            if (Settings.Current.WindowMaximized)
            {
                presenter.Maximize();
            }
            else
            {
                presenter.Restore();
            }
        }
        WinApi.SetForegroundWindow(WinApi.HWND);
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
            var settingsPage = new SettingsPage();
            settingsPage.OnProgress += Page_OnProgress;
            frameMain.Content = settingsPage;
            Page_OnChangeBackground(null, (null, AnimationDirection.None));
        }
    }

    private async void CreateEntryFromFiles_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();

        // Need to initialize the picker object with the hwnd / IInitializeWithWindow
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinApi.HWND);

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
            var newEntry = await EntryHelper.CreateEntryFromFiles(XamlRoot, files, withConfirm: true, SetProgress);
            if (newEntry != null)
            {
                Settings.Current.Entries.Add(newEntry);
                DoomList.SelectedItem = newEntry;
                Settings.Save();
            }
        }
    }

    private async void CreateMod_Click(object sender, SplitButtonClickEventArgs e)
    {
        var initial = new EditEntryDialogResult(new DoomEntry());
        if (await EditEntryDialog.ShowAsync(XamlRoot, initial, EditDialogMode.Create) is EditEntryDialogResult result)
        {
            var newEntry = new DoomEntry()
            {
                Id = Guid.NewGuid().ToString(),
                Name = result.name,
                Description = result.description,
                LongDescription = result.longDescription,
                GZDoomPath = result.gZDoomPath,
                IWadFile = result.iWadFile,
                SteamGame = result.steamGame,
                UniqueConfig = result.uniqueConfig,
                UniqueSavesFolder = result.uniqueSavesFolder,
                SelectedImageIndex = 0,
            };
            Settings.Current.Entries.Add(newEntry);
            DoomList.SelectedItem = newEntry;
            Settings.Save();
        }
    }

    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();

        // Need to initialize the picker object with the hwnd / IInitializeWithWindow
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinApi.HWND);

        // Now we can use the picker object as normal
        picker.FileTypeFilter.Add(".gzdl");
        picker.FileTypeFilter.Add(".zip");

        var files = await picker.PickMultipleFilesAsync();

        if (files.Any())
        {
            await ImportEntriesFromGZDLFiles(files, withConfirm: true);
        }
    }

    private async Task CreateShortcut(DoomEntry entry)
    {
        var picker = new Windows.Storage.Pickers.FileSavePicker();

        // Need to initialize the picker object with the hwnd / IInitializeWithWindow
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinApi.HWND);

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

    private async Task ExportEntry(DoomEntry entry)
    {
        var picker = new Windows.Storage.Pickers.FileSavePicker();

        // Need to initialize the picker object with the hwnd / IInitializeWithWindow
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinApi.HWND);

        // Now we can use the picker object as normal
        picker.FileTypeChoices.Add("Сборка GZDoomLauncher", new List<string>() { ".gzdl" });
        picker.SuggestedFileName = entry.Name;
        picker.CommitButtonText = "Экспортировать";

        var file = await picker.PickSaveFileAsync();
        if (file != null)
        {
            await EntryHelper.ExportToGZDLFile(entry, file, SetProgress);
        }
    }

    public async Task ImportEntriesFromGZDLFiles(IReadOnlyList<StorageFile> files, bool withConfirm)
    {
        DoomEntry? lastAddedEntry = null;
        foreach (var file in files)
        {
            var newEntry = await EntryHelper.ImportFromGZDLFile(XamlRoot, file, withConfirm, SetProgress);
            if (newEntry != null)
            {
                Settings.Current.Entries.Add(newEntry);
                lastAddedEntry = newEntry;
            }
        }
        if (lastAddedEntry != null)
        {
            DoomList.SelectedItem = lastAddedEntry;
            Settings.Save();
        }
    }

    public async Task ImportEntryFromDoomWorldId(string wadId, bool withConfirm)
    {
        SetProgress($"Получение информации...");
        var wadInfo = await WebAPI.Current.GetDoomWorldWADInfo(wadId);
        if (wadInfo != null)
        {
            var newEntry = await EntryHelper.ImportFromDoomWorld(XamlRoot, wadInfo, withConfirm, SetProgress);
            if (newEntry != null)
            {
                Settings.Current.Entries.Add(newEntry);
                DoomList.SelectedItem = newEntry;
                Settings.Save();
            }
        }
        SetProgress(null);
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
                var gzdlFiles = new List<StorageFile>();
                var otherFiles = new List<StorageFile>();
                foreach (var item in items)
                {
                    if (item is StorageFile file)
                    {
                        var ext = Path.GetExtension(file.Name).ToLowerInvariant();
                        if (ext == ".gzdl" || ext == ".zip")
                        {
                            gzdlFiles.Add(file);
                        }
                        else if (FileHelper.SupportedModExtensions.Contains(ext) || FileHelper.SupportedImageExtensions.Contains(ext))
                        {
                            otherFiles.Add(file);
                        }
                    }
                }
                if (gzdlFiles.Any())
                {
                    await ImportEntriesFromGZDLFiles(gzdlFiles, withConfirm: true);
                }
                if (otherFiles.Any())
                {
                    var newEntry = await EntryHelper.CreateEntryFromFiles(XamlRoot, otherFiles, withConfirm: true, SetProgress);
                    if (newEntry != null)
                    {
                        Settings.Current.Entries.Add(newEntry);
                        DoomList.SelectedItem = newEntry;
                        Settings.Save();
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
