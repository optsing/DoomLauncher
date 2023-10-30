using CommunityToolkit.Mvvm.ComponentModel;
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

public partial class DoomPageViewModel : ObservableObject
{
    private readonly DispatcherTimer timerSlideshow;

    public DoomPageViewModel(DispatcherTimer timer)
    {
        timerSlideshow = timer;
    }

    [ObservableProperty]
    private int currentTicksToSlideshow;

    private bool isSlideshowEnabled;
    public bool IsSlideshowEnabled
    {
        get => isSlideshowEnabled;
        set
        {
            if (SetProperty(ref isSlideshowEnabled, value))
            {
                if (value)
                {
                    timerSlideshow.Start();
                }
                else
                {
                    timerSlideshow.Stop();
                }
            }
        }
    }
} 

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class DoomPage : Page
{
    private readonly TimeSpan SlideshowInterval = TimeSpan.FromSeconds(1);
    private readonly int TicksToSlideshow = 10;
    private readonly DispatcherTimer timerSlideshow = new();
    public DoomEntry entry = new();

    public DoomPageViewModel ViewModel;

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
        if (e.Parameter is DoomEntry entry)
        {
            this.entry = entry;
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
                }
            };
            browseItem.Click += Append_Click;
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
            if (Settings.Current.FavoriteFiles.Any())
            {
                foreach (var filePath in Settings.Current.FavoriteFiles)
                {
                    var item = new MenuFlyoutItem()
                    {
                        Text = GetFileTitle(filePath),
                    };
                    item.Click += async (object sender, RoutedEventArgs e) =>
                    {
                        var fullPath = Path.GetFullPath(filePath, FileHelper.ModsFolderPath);
                        if (File.Exists(fullPath))
                        {
                            var file = await StorageFile.GetFileFromPathAsync(fullPath);
                            await AddFiles(new StorageFile[] { file });
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

    private async void Append_Click(object sender, RoutedEventArgs e)
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
        if (files.Any())
        {
            await AddFiles(files);
        }
    }

    private async void AddBackground_Click(object sender, RoutedEventArgs e)
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
        if (files.Any())
        {
            await AddImages(files);
        }
    }

    private void SetSelectedImageIndex(int ind, AnimationDirection direction)
    {
        if (entry.ImageFiles.Any())
        {
            if (ind < 0)
            {
                ind = entry.ImageFiles.Count - 1;
            }
            else if (ind >= entry.ImageFiles.Count)
            {
                ind = 0;
            }
            entry.SelectedImageIndex = ind;
            var imagePath = Path.GetFullPath(entry.ImageFiles[entry.SelectedImageIndex], FileHelper.ImagesFolderPath);
            EventBus.ChangeBackground(this, imagePath, direction);
        }
        else
        {
            entry.SelectedImageIndex = 0;
            EventBus.ChangeBackground(this, null, direction);
        }
    }

    private void PreviousBackground_Click(object sender, RoutedEventArgs e)
    {
        SetSelectedImageIndex(entry.SelectedImageIndex - 1, direction: AnimationDirection.Previous);
    }

    private void NextBackground_Click(object sender, RoutedEventArgs e)
    {
        SetSelectedImageIndex(entry.SelectedImageIndex + 1, direction: AnimationDirection.Next);
    }

    private void Timer_Tick(object? sender, object e)
    {
        if (ViewModel.CurrentTicksToSlideshow < TicksToSlideshow)
        {
            ViewModel.CurrentTicksToSlideshow += 1;
        }
        else
        {
            SetSelectedImageIndex(entry.SelectedImageIndex + 1, direction: AnimationDirection.Next);
            ViewModel.CurrentTicksToSlideshow = 0;
        }
    }

    private async Task AddFiles(IReadOnlyList<StorageFile> files)
    {
        foreach (var file in files)
        {
            EventBus.Progress(this, $"Копирование: {file.Name}");
            await FileHelper.CopyFileWithConfirmation(file, FileHelper.ModsFolderPath);
            if (!entry.ModFiles.Contains(file.Name))
            {
                entry.ModFiles.Add(file.Name);
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
            if (!entry.ModFiles.Contains(file.Name))
            {
                entry.ImageFiles.Add(file.Name);
                hasAddedImages = true;
            }
        }
        EventBus.Progress(this, null);
        if (hasAddedImages)
        {
            SetSelectedImageIndex(entry.ImageFiles.Count - 1, direction: AnimationDirection.Next);
            SetSlideshow();
        }
    }

    private void OpenContainFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el)
        {
            if (el.DataContext is string filePath)
            {
                Process.Start("explorer.exe", "/select," + Path.GetFullPath(filePath, FileHelper.ModsFolderPath));
            }
        }
    }

    private void ToggleFavoriteFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el)
        {
            if (el.DataContext is string filePath)
            {
                if (Settings.Current.FavoriteFiles.Contains(filePath))
                {
                    Settings.Current.FavoriteFiles.Remove(filePath);
                } else
                {
                    Settings.Current.FavoriteFiles.Add(filePath);
                }
            }
        }
    }

    private async void RemoveFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el)
        {
            if (el.DataContext is string filePath)
            {
                var fileName = GetFileTitle(filePath);
                if (await DialogHelper.ShowAskAsync("Удаление ссылки на файл", $"Вы уверены, что хотите удалить ссылку на файл '{fileName}'?", "Удалить", "Отмена"))
                {
                    entry.ModFiles.Remove(filePath);
                }
            }
        }
    }

    private async void RemoveBackground_Click(object sender, RoutedEventArgs e)
    {
        if (entry.SelectedImageIndex >= 0 && entry.SelectedImageIndex < entry.ImageFiles.Count)
        {
            ViewModel.IsSlideshowEnabled = false;
            var selectedImageIndex = entry.SelectedImageIndex;
            if (await DialogHelper.ShowAskAsync("Удаление фона", $"Вы уверены, что хотите удалить текущий фон?", "Удалить", "Отмена"))
            {
                entry.ImageFiles.RemoveAt(selectedImageIndex);
                SetSelectedImageIndex(selectedImageIndex, direction: AnimationDirection.Next);
            }
            SetSlideshow();
        }
    }

    public static string GetFileTitle(string filePath)
    {
        return Path.GetFileName(filePath);
    }

    public static BitmapImage? GetCurrentBackground(IList<string> list, int selectedImageIndex)
    {
        if (list.Any() && selectedImageIndex < list.Count)
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
                if (mods.Any())
                {
                    await AddFiles(mods);
                }
                if (images.Any())
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
        ViewModel.IsSlideshowEnabled = entry.Slideshow && entry.ImageFiles.Count > 1;
    }

    private void Slideshow_Click(object sender, RoutedEventArgs e)
    {
        entry.Slideshow = !entry.Slideshow;
        ViewModel.CurrentTicksToSlideshow = 0;
        SetSlideshow();
    }
}
