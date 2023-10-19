using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
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

public partial class RootPageViewModel : ObservableObject
{
    [ObservableProperty]
    private string? caption = null;

    [ObservableProperty]
    private BitmapImage? background = null;

    [ObservableProperty]
    private DoomEntry? currentEntry = null;

    [ObservableProperty]
    private string? progressText = null;

    [ObservableProperty]
    private bool isLeftDropHelperVisible;

    [ObservableProperty]
    private bool isRightDropHelperVisible;
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

    private RootPageViewModel ViewModel { get; set; }

    public RootPage()
    {
        InitializeComponent();

        EventBus.OnProgress += EventBus_OnProgress;
        EventBus.OnChangeBackground += EventBus_OnChangeBackground;
        EventBus.OnChangeCaption += EventBus_OnChangeCaption;
        EventBus.OnDropHelper += EventBus_OnDropHelper;

        if (Settings.IsCustomTitlebar)
        {
            var appWindow = AppWindow.GetFromWindowId(WinApi.WindowId);
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
        ViewModel = new()
        {
            CurrentEntry = Settings.Current.Entries.ElementAtOrDefault(Settings.Current.SelectedModIndex),
        };
        NavigateToEntry(ViewModel.CurrentEntry);
    }

    private void EventBus_OnDropHelper(object? sender, bool e)
    {
        ViewModel.IsRightDropHelperVisible = e;
    }

    private void EventBus_OnChangeCaption(object? sender, string? e)
    {
        ViewModel.Caption = e;
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
        if (InputNonClientPointerSource.GetForWindowId(WinApi.WindowId) is InputNonClientPointerSource nonClientSource)
        {
            var scaleAdjustment = XamlRoot.RasterizationScale;
            var dragRect = new Windows.Graphics.RectInt32()
            {
                X = (int)(titleBar.ActualOffset.X * scaleAdjustment),
                Y = (int)(titleBar.ActualOffset.Y * scaleAdjustment),
                Width = (int)(titleBar.ActualWidth * scaleAdjustment),
                Height = (int)(titleBar.ActualHeight * scaleAdjustment),
            };
            nonClientSource.SetRegionRects(NonClientRegionKind.Caption, new[] { dragRect });
        }
    }

    private readonly SemaphoreSlim semaphoreAnimation = new(1, 1);
    private async void EventBus_OnChangeBackground(object? sender, (string? imagePath, AnimationDirection direction) e)
    {
        await semaphoreAnimation.WaitAsync();
        var bitmap = string.IsNullOrEmpty(e.imagePath) ? null : await BitmapHelper.CreateBitmapFromFile(e.imagePath);
        bool hasPrevBitmap = ViewModel.Background != null;
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
        ViewModel.Background = bitmap;
        if (bitmap != null)
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
            ViewModel.ProgressText = null;
        }
        else
        {
            ViewModel.ProgressText = text;
        }
    }

    private void EventBus_OnProgress(object? sender, string? e)
    {
        SetProgress(e);
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
            if (entry == ViewModel.CurrentEntry)
            {
                SetCurrentEntry(null);
            }
            Settings.Current.Entries.Remove(entry);
            Settings.Save();
            await JumpListHelper.Update();
        }
    }

    private async Task EditEntry(DoomEntry entry)
    {
        if (await EditEntryDialog.ShowAsync(XamlRoot, entry, EditDialogMode.Edit) is EditEntryDialogViewModel result)
        {
            result.UpdateEntry(entry);
            Settings.Save();
            await JumpListHelper.Update();
        }
    }

    private async void CopyEntry(DoomEntry entry)
    {
        if (await EditEntryDialog.ShowAsync(XamlRoot, entry, EditDialogMode.Copy) is EditEntryDialogViewModel result)
        {
            var newEntry = new DoomEntry()
            {
                Id = Guid.NewGuid().ToString(),
                Created = DateTime.Now,
                SelectedImageIndex = entry.SelectedImageIndex,
                ModFiles = new(entry.ModFiles),
                ImageFiles = new(entry.ImageFiles),
            };
            result.UpdateEntry(newEntry);
            AddEntries(new[] { newEntry });
        }
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el)
        {
            if (el.DataContext is DoomEntry entry)
            {
                LaunchEntry(entry, forceClose: false);
            }
        }
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
        SetCurrentEntry(entry);
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
                await LaunchHelper.CurrentProcess.WaitForExitAsync();
                entry.PlayTime = (entry.PlayTime ?? new()) + (LaunchHelper.CurrentProcess.ExitTime - LaunchHelper.CurrentProcess.StartTime);
                if (LaunchHelper.CurrentProcess.ExitCode != 0)
                {
                    var error = await LaunchHelper.CurrentProcess.StandardError.ReadToEndAsync();
                    if (string.IsNullOrEmpty(error))
                    {
                        await AskDialog.ShowAsync(XamlRoot, "Ошибка при запуске", $"Игра завершилась с ошибкой, код ошибки: {LaunchHelper.CurrentProcess.ExitCode}", "", "Отмена");
                    }
                    else
                    {
                        await AskDialog.ShowAsync(XamlRoot, "Ошибка при запуске", $"Игра завершилась с ошибкой, вывод:\n{error}", "", "Отмена");
                    }
                }
            }
        }
        else if (result == LaunchResult.AlreadyLaunched && LaunchHelper.CurrentProcess != null)
        {
            await AskDialog.ShowAsync(XamlRoot, "Ошибка при запуске", "Игра уже запущена, закройте текущую игру", "", "Отмена");
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

    private void ButtonMenu_Click(object sender, RoutedEventArgs e)
    {
        swMain.IsPaneOpen = !swMain.IsPaneOpen;
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (frameMain.Content is not SettingsPage)
        {
            frameMain.Navigate(typeof(SettingsPage));
            SetCurrentEntry(null);
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
            var newEntry = await EntryHelper.CreateFromFiles(XamlRoot, files, withConfirm: true, SetProgress);
            if (newEntry != null)
            {
                AddEntries(new[] { newEntry });
            }
        }
    }

    private async void CreateMod_Click(object sender, SplitButtonClickEventArgs e)
    {
        if (await EditEntryDialog.ShowAsync(XamlRoot, EditDialogMode.Create) is EditEntryDialogViewModel result)
        {
            var newEntry = new DoomEntry()
            {
                Id = Guid.NewGuid().ToString(),
                Created = DateTime.Now,
                SelectedImageIndex = 0,
            };
            result.UpdateEntry(newEntry);
            AddEntries(new[] { newEntry });
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
        var addedEntries = new List<DoomEntry>();
        foreach (var file in files)
        {
            var newEntry = await EntryHelper.ImportFromGZDLFile(XamlRoot, file, withConfirm, SetProgress);
            if (newEntry != null)
            {
                addedEntries.Add(newEntry);
            }
        }
        AddEntries(addedEntries);
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
                AddEntries(new[] { newEntry });
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
                            ViewModel.IsLeftDropHelperVisible = true;
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
        ViewModel.IsLeftDropHelperVisible = false;
    }

    private void LeftDropHelper_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void LeftDropHelper_Drop(object? sender, DragEventArgs e)
    {
        ViewModel.IsLeftDropHelperVisible = false;
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
                    var newEntry = await EntryHelper.CreateFromFiles(XamlRoot, otherFiles, withConfirm: true, SetProgress);
                    if (newEntry != null)
                    {
                        AddEntries(new[] { newEntry });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }
    }

    private void RightDropHelper_DragOver(object sender, DragEventArgs e)
    {
        EventBus.RightDragOver(this, e);
    }

    private void RightDropHelper_Drop(object sender, DragEventArgs e)
    {
        ViewModel.IsRightDropHelperVisible = false;
        EventBus.RightDrop(this, e);
    }

    private void RightDropHelper_DragEnter(object sender, DragEventArgs e)
    {
        EventBus.RightDragEnter(this, e);
    }

    private void RightDropHelper_DragLeave(object sender, DragEventArgs e)
    {
        ViewModel.IsRightDropHelperVisible = false;
    }

    public void AddEntries(IEnumerable<DoomEntry> entries)
    {
        if (entries.Any())
        {
            foreach (var entry in entries)
            {
                Settings.Current.Entries.Insert(0, entry);
            }
            SetCurrentEntry(entries.Last());
            Settings.Save();
        }
    }

    public void SetCurrentEntry(DoomEntry? entry)
    {
        if (entry != ViewModel.CurrentEntry)
        {
            ViewModel.CurrentEntry = entry;
            NavigateToEntry(entry);
        }
    }

    public void NavigateToEntry(DoomEntry? entry)
    {
        if (entry != null)
        {
            Settings.Current.SelectedModIndex = Settings.Current.Entries.IndexOf(entry);
            frameMain.Navigate(typeof(DoomPage), entry);
        }
        else if (frameMain.Content is not SettingsPage)
        {
            frameMain.Navigate(typeof(NotSelectedPage));
        }
        if (swMain.DisplayMode == SplitViewDisplayMode.Overlay)
        {
            swMain.IsPaneOpen = false;
        }
    }

    private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListView listView)
        {
            if (listView.SelectedItem is DoomEntry entry)
            {
                SetCurrentEntry(entry);
            }
            else
            {
                SetCurrentEntry(null);
            }
        }
    }
}
