﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoomLauncher.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DoomLauncher;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class DoomPage : Page
{
    private readonly TimeSpan SlideshowInterval = TimeSpan.FromSeconds(1);
    private readonly int TicksToSlideshow = 10;
    private readonly DispatcherTimer timerSlideshow = new();

    public DoomPageViewModel ViewModel { get; set; }

    public DoomPage()
    {
        InitializeComponent();
        
        timerSlideshow.Interval = SlideshowInterval;
        timerSlideshow.Tick += Timer_Tick;

        ViewModel = new DoomPageViewModel(timerSlideshow);

        EventBus.OnRightDragEnter += Root_DragEnter;
        EventBus.OnRightDragOver += DropHelper_DragOver;
        EventBus.OnRightDrop += DropHelper_Drop;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is DoomEntryViewModel entry)
        {
            ViewModel.Entry = entry;
            SetSlideshow();
            SetSelectedImageIndex(entry.SelectedImageIndex, direction: AnimationDirection.None);
            EventBus.ChangeCaption(this, entry.Name);
        }
        base.OnNavigatedTo(e);
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        EventBus.OnRightDragEnter -= Root_DragEnter;
        EventBus.OnRightDragOver -= DropHelper_DragOver;
        EventBus.OnRightDrop -= DropHelper_Drop;
        timerSlideshow.Tick -= Timer_Tick;
        timerSlideshow.Stop();
    }

    private void MenuFlyout_Opening(object sender, object e)
    {
        if (sender is MenuFlyout menu)
        {
            menu.Items.Clear();
            var browseItem = new MenuFlyoutItem()
            {
                Text = "Выбрать на устройстве",
                Icon = new FontIcon()
                {
                    Glyph = "\uEC50",
                    FontSize = 14,
                },
                Command = AddLocalFileCommand,
            };
            menu.Items.Add(browseItem);
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(new MenuFlyoutItem()
            {
                Text = "Избранное",
                IsEnabled = false,
                Icon = new FontIcon()
                {
                    Glyph = "\uE735",
                    FontSize = 14,
                }
            });
            if (SettingsViewModel.Current.FavoriteFiles.Count > 0)
            {
                foreach (var filePath in SettingsViewModel.Current.FavoriteFiles)
                {
                    var item = new MenuFlyoutItem()
                    {
                        Text = FileHelper.GetFileTitle(filePath),
                    };
                    item.Click += async (object sender, RoutedEventArgs e) =>
                    {
                        var fullPath = Path.GetFullPath(filePath, FileHelper.ModsFolderPath);
                        if (File.Exists(fullPath))
                        {
                            var file = await StorageFile.GetFileFromPathAsync(fullPath);
                            await AddFiles([file]);
                        }
                    };
                    menu.Items.Add(item);
                }
            }
            else
            {
                menu.Items.Add(new MenuFlyoutItem()
                {
                    Text = "(пусто)",
                    IsEnabled = false,
                });
            }
        }
    }

    [RelayCommand]
    private async Task AddLocalFile()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();

        // Need to initialize the picker object with the hwnd / IInitializeWithWindow
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinApi.HWND);

        // Now we can use the picker object as normal
        foreach (var fileExtension in FileHelper.SupportedModExtensions)
        {
            picker.FileTypeFilter.Add(fileExtension);
        }

        var files = await picker.PickMultipleFilesAsync();
        if (files.Count > 0)
        {
            await AddFiles(files);
        }
    }

    [RelayCommand]
    private async Task AddBackground()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();

        // Need to initialize the picker object with the hwnd / IInitializeWithWindow
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinApi.HWND);

        // Now we can use the picker object as normal
        foreach (var fileExtension in FileHelper.SupportedImageExtensions)
        {
            picker.FileTypeFilter.Add(fileExtension);
        }

        var files = await picker.PickMultipleFilesAsync();
        if (files.Count > 0)
        {
            await AddImages(files);
        }
    }

    private void SetSelectedImageIndex(int ind, AnimationDirection direction)
    {
        if (ViewModel.Entry.ImageFiles.Count > 0)
        {
            if (ind < 0)
            {
                ind = ViewModel.Entry.ImageFiles.Count - 1;
            }
            else if (ind >= ViewModel.Entry.ImageFiles.Count)
            {
                ind = 0;
            }
            ViewModel.Entry.SelectedImageIndex = ind;
            var imagePath = Path.GetFullPath(ViewModel.Entry.ImageFiles[ViewModel.Entry.SelectedImageIndex], FileHelper.ImagesFolderPath);
            EventBus.ChangeBackground(this, imagePath, direction);
        }
        else
        {
            ViewModel.Entry.SelectedImageIndex = 0;
            EventBus.ChangeBackground(this, null, direction);
        }
    }

    [RelayCommand]
    private void GoToPreviousBackground()
    {
        SetSelectedImageIndex(ViewModel.Entry.SelectedImageIndex - 1, direction: AnimationDirection.Previous);
    }

    [RelayCommand]
    private void GoToNextBackground()
    {
        SetSelectedImageIndex(ViewModel.Entry.SelectedImageIndex + 1, direction: AnimationDirection.Next);
    }

    private void Timer_Tick(object? sender, object e)
    {
        if (ViewModel.CurrentTicksToSlideshow < TicksToSlideshow)
        {
            ViewModel.CurrentTicksToSlideshow += 1;
        }
        else
        {
            SetSelectedImageIndex(ViewModel.Entry.SelectedImageIndex + 1, direction: AnimationDirection.Next);
            ViewModel.CurrentTicksToSlideshow = 0;
        }
    }

    private async Task AddFiles(IReadOnlyList<StorageFile> files)
    {
        foreach (var file in files)
        {
            EventBus.Progress(this, $"Копирование: {file.Name}");
            await FileHelper.CopyFileWithConfirmation(file, FileHelper.ModsFolderPath);
            if (!ViewModel.Entry.ModFiles.Contains(file.Name))
            {
                ViewModel.Entry.ModFiles.Add(file.Name);
            }
        }
        EventBus.Progress(this, null);
    }

    private async Task AddImages(IReadOnlyList<StorageFile> files)
    {
        bool hasAddedImages = false;
        foreach (var file in files)
        {
            EventBus.Progress(this, $"Копирование: {file.Name}");
            await FileHelper.CopyFileWithConfirmation(file, FileHelper.ImagesFolderPath);
            if (!ViewModel.Entry.ModFiles.Contains(file.Name))
            {
                ViewModel.Entry.ImageFiles.Add(file.Name);
                hasAddedImages = true;
            }
        }
        EventBus.Progress(this, null);
        if (hasAddedImages)
        {
            SetSelectedImageIndex(ViewModel.Entry.ImageFiles.Count - 1, direction: AnimationDirection.Next);
            SetSlideshow();
        }
    }

    [RelayCommand]
    private async Task RemoveBackground()
    {
        if (ViewModel.Entry.SelectedImageIndex >= 0 && ViewModel.Entry.SelectedImageIndex < ViewModel.Entry.ImageFiles.Count)
        {
            ViewModel.IsSlideshowEnabled = false;
            var selectedImageIndex = ViewModel.Entry.SelectedImageIndex;
            if (await DialogHelper.ShowAskAsync("Удаление фона", $"Вы уверены, что хотите удалить текущий фон?", "Удалить", "Отмена"))
            {
                ViewModel.Entry.ImageFiles.RemoveAt(selectedImageIndex);
                SetSelectedImageIndex(selectedImageIndex, direction: AnimationDirection.Next);
            }
            SetSlideshow();
        }
    }

    public static BitmapImage? GetCurrentBackground(IList<string> list, int selectedImageIndex)
    {
        if (list.Count > 0 && selectedImageIndex < list.Count)
        {
            return new BitmapImage(new Uri(list[selectedImageIndex]));
        }
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
                        if (FileHelper.SupportedModExtensions.Contains(ext) || FileHelper.SupportedImageExtensions.Contains(ext))
                        {
                            EventBus.DropHelper(this, true);
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

    private void DropHelper_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void DropHelper_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                var mods = new List<StorageFile>();
                var images = new List<StorageFile>();
                foreach (var item in items)
                {
                    if (item is StorageFile file)
                    {
                        var ext = Path.GetExtension(file.Name).ToLowerInvariant();
                        if (FileHelper.SupportedModExtensions.Contains(ext))
                        {
                            mods.Add(file);
                        }
                        else if (FileHelper.SupportedImageExtensions.Contains(ext))
                        {
                            images.Add(file);
                        }
                    }
                }
                if (mods.Count > 0)
                {
                    await AddFiles(mods);
                }
                if (images.Count > 0)
                {
                    await AddImages(images);
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }
    }

    private void SetSlideshow()
    {
        ViewModel.IsSlideshowEnabled = ViewModel.Entry.Slideshow && ViewModel.Entry.ImageFiles.Count > 1;
    }

    [RelayCommand]
    private void ToggleSlideshow()
    {
        ViewModel.Entry.Slideshow = !ViewModel.Entry.Slideshow;
        ViewModel.CurrentTicksToSlideshow = 0;
        SetSlideshow();
    }
}
