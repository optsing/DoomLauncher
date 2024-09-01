using CommunityToolkit.Mvvm.Input;
using DoomLauncher.Helpers;
using DoomLauncher.ViewModels;
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

    private RootPageViewModel ViewModel { get; set; }

    public RootPage()
    {
        InitializeComponent();

        EventBus.OnProgress += EventBus_OnProgress;
        EventBus.OnChangeBackground += EventBus_OnChangeBackground;
        EventBus.OnChangeCaption += EventBus_OnChangeCaption;
        EventBus.OnDropHelper += EventBus_OnDropHelper;

        if (SettingsViewModel.IsCustomTitleBar)
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
            CurrentEntry = SettingsViewModel.Current.Entries.ElementAtOrDefault(SettingsViewModel.Current.SelectedModIndex),
        };
        InitializeFilterMenu();
        NavigateToEntry(ViewModel.CurrentEntry);
    }

    private void InitializeFilterMenu()
    {
        if (ViewModel is RootPageViewModel vm)
        {
            foreach (var order in vm.SortOrders)
            {
                var item = new RadioMenuFlyoutItem
                {
                    Text = order.Value,
                    IsChecked = vm.SortOrder.Key == order.Key,
                };
                item.Click += (sender, e) => vm.SortOrder = order;
                filterMenuFlyout.Items.Add(item);
            }
        }
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
            nonClientSource.SetRegionRects(NonClientRegionKind.Caption, [dragRect]);
        }
    }

    private readonly SemaphoreSlim semaphoreAnimation = new(1, 1);
    private async void EventBus_OnChangeBackground(object? sender, (string? imagePath, AnimationDirection direction) e)
    {
        await semaphoreAnimation.WaitAsync();
        var bitmap = string.IsNullOrEmpty(e.imagePath) ? null : await BitmapHelper.CreateBitmapFromFile(e.imagePath, isPreview: false);
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

    [RelayCommand]
    private async Task RemoveEntry(DoomEntryViewModel? entry)
    {
        if (entry == null)
        {
            return;
        }
        if (await DialogHelper.ShowAskAsync(Strings.Resources.DialogEntryRemoveTitle, Strings.Resources.DialogEntryRemoveText(entry.Name), Strings.Resources.DialogRemoveAction, Strings.Resources.DialogCancelAction))
        {
            if (entry == ViewModel.CurrentEntry)
            {
                SetCurrentEntry(null);
            }
            SettingsViewModel.Current.Entries.Remove(entry);
            SettingsViewModel.Current.Save();
            await JumpListHelper.Update();
        }
    }

    [RelayCommand]
    private async Task EditEntry(DoomEntryViewModel? entry)
    {
        if (entry == null)
        {
            return;
        }
        var result = await DialogHelper.ShowEditEntryAsync(entry, EditDialogMode.Edit);
        if (result.Result == ContentDialogResult.Primary)
        {
            result.ViewModel.UpdateEntry(entry);
            SettingsViewModel.Current.Save();
            await JumpListHelper.Update();
        }
        else if (result.Result == ContentDialogResult.Secondary)
        {
            var newEntry = new DoomEntryViewModel()
            {
                Id = Guid.NewGuid().ToString(),
                Created = DateTime.Now,
                SelectedImageIndex = entry.SelectedImageIndex,
                ModFiles = new(entry.ModFiles),
                ImageFiles = new(entry.ImageFiles),
            };
            result.ViewModel.UpdateEntry(newEntry);
            AddEntries([newEntry]);
        }
    }

    public void LaunchEntryById(string entryId, bool forceClose)
    {
        var entry = SettingsViewModel.Current.Entries.FirstOrDefault(entry => string.Equals(entry.Id, entryId));
        LaunchEntry(entry, forceClose);
    }

    public void LaunchEntryByName(string entryName, bool forceClose)
    {
        var entry = SettingsViewModel.Current.Entries.FirstOrDefault(entry => string.Equals(entry.Name, entryName));
        LaunchEntry(entry, forceClose);
    }

    [RelayCommand]
    private void LaunchEntry(DoomEntryViewModel? entry)
    {
        LaunchEntry(entry, false);
    }

    private async void LaunchEntry(DoomEntryViewModel? entry, bool forceClose)
    {
        if (entry == null)
        {
            await DialogHelper.ShowAskAsync(Strings.Resources.DialogLaunchErrorTitle, Strings.Resources.DialogLaunchErrorNoEntryText, "", Strings.Resources.DialogCancelAction);
            return;
        }
        SetCurrentEntry(entry);
        var result = LaunchHelper.LaunchEntry(entry);
        if (result == LaunchResult.Success && LaunchHelper.CurrentProcess != null)
        {
            entry.LastLaunch = DateTime.Now;
            await JumpListHelper.Update();
            if (SettingsViewModel.Current.CloseOnLaunch || forceClose)
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
                        await DialogHelper.ShowAskAsync(Strings.Resources.DialogLaunchErrorTitle, Strings.Resources.DialogLaunchErrorCodeText(LaunchHelper.CurrentProcess.ExitCode), "", Strings.Resources.DialogCancelAction);
                    }
                    else
                    {
                        await DialogHelper.ShowAskAsync(Strings.Resources.DialogLaunchErrorTitle, Strings.Resources.DialogLaunchErrorText(error), "", Strings.Resources.DialogCancelAction);
                    }
                }
            }
        }
        else if (result == LaunchResult.AlreadyLaunched && LaunchHelper.CurrentProcess != null)
        {
            await DialogHelper.ShowAskAsync(Strings.Resources.DialogLaunchErrorTitle, Strings.Resources.DialogLaunchErrorAlreadyLaunchedText, "", Strings.Resources.DialogCancelAction);
        }
        else if (result == LaunchResult.PathNotValid)
        {
            if (await DialogHelper.ShowAskAsync(Strings.Resources.DialogLaunchErrorTitle, Strings.Resources.DialogLaunchErrorGZDoomPathNotValidText, Strings.Resources.DialogEditAction, Strings.Resources.DialogCancelAction))
            {
                await EditEntry(entry);
            }
        }
        else
        {
            await DialogHelper.ShowAskAsync(Strings.Resources.DialogLaunchErrorTitle, Strings.Resources.DialogLaunchErrorUnknownText, "", Strings.Resources.DialogCancelAction);
        }
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        swMain.IsPaneOpen = !swMain.IsPaneOpen;
    }

    [RelayCommand]
    private void NavigateToSettingsPage()
    {
        if (frameMain.Content is not SettingsPage)
        {
            frameMain.Navigate(typeof(SettingsPage));
            SetCurrentEntry(null);
        }
    }

    [RelayCommand]
    private async Task CreateEntryFromFiles()
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

        if (files.Count > 0)
        {
            var newEntry = await EntryHelper.CreateFromFiles(files, withConfirm: true, SetProgress);
            if (newEntry != null)
            {
                AddEntries([newEntry]);
            }
        }
    }

    [RelayCommand]
    private async Task CreateEntryEmpty()
    {
        if (await DialogHelper.ShowEditEntryAsync(EditDialogMode.Create) is var result && result.Result == ContentDialogResult.Primary)
        {
            var newEntry = new DoomEntryViewModel()
            {
                Id = Guid.NewGuid().ToString(),
                Created = DateTime.Now,
                SelectedImageIndex = 0,
            };
            result.ViewModel.UpdateEntry(newEntry);
            AddEntries([newEntry]);
        }
    }

    [RelayCommand]
    private async Task ImportEntries()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();

        // Need to initialize the picker object with the hwnd / IInitializeWithWindow
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinApi.HWND);

        // Now we can use the picker object as normal
        picker.FileTypeFilter.Add(".gzdl");
        picker.FileTypeFilter.Add(".zip");

        var files = await picker.PickMultipleFilesAsync();

        if (files.Count > 0)
        {
            await ImportEntriesFromGZDLFiles(files, withConfirm: true);
        }
    }

    [RelayCommand]
    private async Task CreateShortcutEntry(DoomEntryViewModel? entry)
    {
        if (entry == null)
        {
            return;
        }
        var picker = new Windows.Storage.Pickers.FileSavePicker();

        // Need to initialize the picker object with the hwnd / IInitializeWithWindow
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinApi.HWND);

        // Now we can use the picker object as normal
        picker.FileTypeChoices.Add(Strings.Resources.FileTypeShortcutTitle, [".url"]);
        picker.SuggestedFileName = entry.Name;
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
        picker.CommitButtonText = Strings.Resources.CreateShortcut;

        var file = await picker.PickSaveFileAsync();
        if (file == null)
        {
            return;
        }

        SetProgress(Strings.Resources.ProgressCreatingShortcut);
        await FileHelper.CreateEntryShortcut(entry, file);
        SetProgress(null);
    }

    [RelayCommand]
    private async Task ExportEntry(DoomEntryViewModel? entry)
    {
        if (entry == null)
        {
            return;
        }
        var picker = new Windows.Storage.Pickers.FileSavePicker();

        // Need to initialize the picker object with the hwnd / IInitializeWithWindow
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinApi.HWND);

        // Now we can use the picker object as normal
        picker.FileTypeChoices.Add(Strings.Resources.FileTypeGZDLTitle, [".gzdl"]);
        picker.SuggestedFileName = entry.Name;
        picker.CommitButtonText = Strings.Resources.Export;

        var file = await picker.PickSaveFileAsync();
        if (file != null)
        {
            await EntryHelper.ExportToGZDLFile(entry, file, SetProgress);
        }
    }

    public async Task ImportEntriesFromGZDLFiles(IReadOnlyList<StorageFile> files, bool withConfirm)
    {
        var addedEntries = new List<DoomEntryViewModel>();
        foreach (var file in files)
        {
            var newEntry = await EntryHelper.ImportFromGZDLFile(file, withConfirm, SetProgress);
            if (newEntry != null)
            {
                addedEntries.Add(newEntry);
            }
        }
        AddEntries(addedEntries);
    }

    public async Task ImportEntryFromDoomWorldId(string wadId, bool withConfirm)
    {
        SetProgress(Strings.Resources.ProgressGettingInformation);
        var wadInfo = await WebAPI.Current.GetDoomWorldWADInfo(wadId);
        if (wadInfo != null)
        {
            var newEntry = await EntryHelper.ImportFromDoomWorld(wadInfo, withConfirm, SetProgress);
            if (newEntry != null)
            {
                AddEntries([newEntry]);
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
                if (gzdlFiles.Count > 0)
                {
                    await ImportEntriesFromGZDLFiles(gzdlFiles, withConfirm: true);
                }
                if (otherFiles.Count > 0)
                {
                    var newEntry = await EntryHelper.CreateFromFiles(otherFiles, withConfirm: true, SetProgress);
                    if (newEntry != null)
                    {
                        AddEntries([newEntry]);
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

    public void AddEntries(List<DoomEntryViewModel> entries)
    {
        if (entries.Count > 0)
        {
            foreach (var entry in entries)
            {
                SettingsViewModel.Current.Entries.Add(entry);
            }
            SetCurrentEntry(entries.Last());
            SettingsViewModel.Current.Save();
        }
    }

    public void SetCurrentEntry(DoomEntryViewModel? entry)
    {
        if (entry != ViewModel.CurrentEntry)
        {
            ViewModel.CurrentEntry = entry;
            NavigateToEntry(entry);
        }
    }

    public void NavigateToEntry(DoomEntryViewModel? entry)
    {
        if (entry != null)
        {
            SettingsViewModel.Current.SelectedModIndex = SettingsViewModel.Current.Entries.IndexOf(entry);
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

    private void ListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is DoomEntryViewModel entry)
        {
            SetCurrentEntry(entry);
        }
    }
}
