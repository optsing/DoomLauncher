using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
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
[INotifyPropertyChanged]
public sealed partial class DoomPage : Page
{
    private readonly TimeSpan SlideshowInterval = TimeSpan.FromSeconds(1);
    private readonly int TicksToSlideshow = 10;
    [ObservableProperty]
    private int currentTicksToSlideshow;
    private readonly DispatcherTimer timerSlideshow = new();

    private readonly DoomEntry entry;
    private readonly IntPtr hWnd;

    public DoomPage(DoomEntry entry, IntPtr hWnd)
    {
        InitializeComponent();
        this.entry = entry;
        this.hWnd = hWnd;

        timerSlideshow.Interval = SlideshowInterval;
        timerSlideshow.Tick += Timer_Tick;

        SetSlideshow();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        SetSelectedImageIndex(entry.SelectedImageIndex, direction: AnimationDirection.None);
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
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
                Text = "Выбрать файлы",
                Icon = new FontIcon()
                {
                    Glyph = "\uEC50",
                    FontSize = 16,
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
                    FontSize = 16,
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

    public event EventHandler<DoomEntry>? OnStart;
    public event EventHandler<DoomEntry>? OnEdit;
    public event EventHandler<DoomEntry>? OnCopy;
    public event EventHandler<DoomEntry>? OnExport;
    public event EventHandler<DoomEntry>? OnCreateShortcut;
    public event EventHandler<DoomEntry>? OnRemove;
    public event EventHandler<string?>? OnProgress;
    public event EventHandler<(BitmapImage? bitmap, AnimationDirection direction)>? OnChangeBackground;

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        OnStart?.Invoke(this, entry);
    }

    private void EditMod_Click(object sender, RoutedEventArgs e)
    {
        OnEdit?.Invoke(this, entry);
    }

    private void CopyMod_Click(object sender, RoutedEventArgs e)
    {
        OnCopy?.Invoke(this, entry);
    }

    private void CreateShortcut_Click(object sender, RoutedEventArgs e)
    {
        OnCreateShortcut?.Invoke(this, entry);
    }

    private void ExportMod_Click(object sender, RoutedEventArgs e)
    {
        OnExport?.Invoke(this, entry);
    }

    private void RemoveMod_Click(object sender, RoutedEventArgs e)
    {
        OnRemove?.Invoke(this, entry);
    }

    private async void Append_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();

        // Need to initialize the picker object with the hwnd / IInitializeWithWindow
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

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
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

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

    private async void SetSelectedImageIndex(int ind, AnimationDirection direction)
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
            var bitmap = await BitmapHelper.CreateBitmapFromFile(imagePath);
            OnChangeBackground?.Invoke(this, (bitmap, direction));
        }
        else
        {
            entry.SelectedImageIndex = 0;
            OnChangeBackground?.Invoke(this, (null, direction));
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
        if (CurrentTicksToSlideshow < TicksToSlideshow)
        {
            CurrentTicksToSlideshow += 1;
        }
        else
        {
            SetSelectedImageIndex(entry.SelectedImageIndex + 1, direction: AnimationDirection.Next);
            CurrentTicksToSlideshow = 0;
        }
    }

    private async Task AddFiles(IReadOnlyList<StorageFile> files)
    {
        foreach (var file in files)
        {
            OnProgress?.Invoke(this, $"Копирование: {file.Name}");
            await FileHelper.CopyFileWithConfirmation(XamlRoot, file, FileHelper.ModsFolderPath);
            if (!entry.ModFiles.Contains(file.Name))
            {
                entry.ModFiles.Add(file.Name);
            }
        }
        OnProgress?.Invoke(this, null);
    }

    private async Task AddImages(IReadOnlyList<StorageFile> files)
    {
        bool hasAddedImages = false;
        foreach (var file in files)
        {
            OnProgress?.Invoke(this, $"Копирование: {file.Name}");
            await FileHelper.CopyFileWithConfirmation(XamlRoot, file, FileHelper.ImagesFolderPath);
            if (!entry.ModFiles.Contains(file.Name))
            {
                entry.ImageFiles.Add(file.Name);
                hasAddedImages = true;
            }
        }
        OnProgress?.Invoke(this, null);
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
                if (await AskDialog.ShowAsync(XamlRoot, "Удаление ссылки на файл", $"Вы уверены, что хотите удалить ссылку на файл '{fileName}'?", "Удалить", "Отмена"))
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
            IsSlideshowEnabled = false;
            var selectedImageIndex = entry.SelectedImageIndex;
            if (await AskDialog.ShowAsync(XamlRoot, "Удаление фона", $"Вы уверены, что хотите удалить текущий фон?", "Удалить", "Отмена"))
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

    private async void DropHelper_Drop(object sender, DragEventArgs e)
    {
        DropHelper.Visibility = Visibility.Collapsed;
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

    private void SetSlideshow()
    {
        IsSlideshowEnabled = entry.Slideshow && entry.ImageFiles.Count > 1;
    }

    private void Slideshow_Click(object sender, RoutedEventArgs e)
    {
        entry.Slideshow = !entry.Slideshow;
        CurrentTicksToSlideshow = 0;
        SetSlideshow();
    }

    private string GZDoomPathToTitle(string path)
    {
        var package = Settings.Current.GZDoomInstalls.FirstOrDefault(package => package.Path == path);
        if (package == null)
        {
            return "Не выбрано";
        }
        return FileHelper.GZDoomPackageToTitle(package);
    }
}
