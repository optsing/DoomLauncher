using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
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
    private readonly TimeSpan SlideshowAnimationDuration = TimeSpan.FromMilliseconds(150);
    private readonly TimeSpan SlideshowInterval = TimeSpan.FromSeconds(10);
    private readonly DispatcherTimer timerSlideshow = new();

    private readonly DoomEntry entry;
    private readonly IntPtr hWnd;
    private readonly string modsFolderPath;
    private readonly string imagesFolderPath;

    public DoomPage(DoomEntry entry, IntPtr hWnd, string dataFolderPath)
    {
        InitializeComponent();
        this.entry = entry;
        this.hWnd = hWnd;
        this.modsFolderPath = Path.Combine(dataFolderPath, "mods");
        this.imagesFolderPath = Path.Combine(dataFolderPath, "images");

        timerSlideshow.Interval = SlideshowInterval;
        timerSlideshow.Tick += Timer_Tick;

        SetSelectedImageIndex(entry.SelectedImageIndex);
        SetSlideshow();
        RefillFileMenu();
    }

    private async void RefillFileMenu()
    {
        mfAppendFile.Items.Clear();
        var browseItem = new MenuFlyoutItem()
        {
            Text = "Выбрать файлы",
            Icon = new FontIcon()
            {
                Glyph = "\uEC50",
            }
        };
        browseItem.Click += Append_Click;
        mfAppendFile.Items.Add(browseItem);
        if (Directory.Exists(modsFolderPath))
        {
            var folder = await StorageFolder.GetFolderFromPathAsync(modsFolderPath);
            var files = (await folder.GetFilesAsync())
                .Where(file => !entry.ModFiles.Any(path => path == file.Path));

            if (files.Any())
            { 
                mfAppendFile.Items.Add(new MenuFlyoutSeparator());
                foreach (var file in files)
                {
                    var item = new MenuFlyoutItem()
                    {
                        Text = GetFileTitle(file.Path),
                    };
                    item.Click += async (object sender, RoutedEventArgs e) =>
                    {
                        await AddFiles(new StorageFile[] { file });
                    };
                    mfAppendFile.Items.Add(item);
                }
            }
        }
    }

    public event EventHandler<DoomEntry>? OnStart;
    public event EventHandler<DoomEntry>? OnEdit;
    public event EventHandler<DoomEntry>? OnCopy;
    public event EventHandler<DoomEntry>? OnExport;
    public event EventHandler<DoomEntry>? OnRemove;
    public event EventHandler<string?>? OnProgress;

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
        foreach (var fileExtension in Settings.SupportedModExtensions)
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
        foreach (var fileExtension in Settings.SupportedImageExtensions)
        {
            picker.FileTypeFilter.Add(fileExtension);
        }

        var files = await picker.PickMultipleFilesAsync();
        if (files.Any())
        {
            await AddImages(files);
        }
    }

    private readonly SemaphoreSlim semaphoreAnimation = new(1, 1);

    private async void SetSelectedImageIndex(int ind, bool toPrevious = false)
    {
        BitmapImage? bitmap;
        bool hasPrevBitmap = imgBackground.Source != null;
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
            var imagePath = Path.GetFullPath(entry.ImageFiles[entry.SelectedImageIndex], imagesFolderPath);
            bitmap = await BitmapHelper.CreateBitmapFromFile(imagePath);
        }
        else
        {
            entry.SelectedImageIndex = 0;
            bitmap = null;
        }
        await semaphoreAnimation.WaitAsync();
        if (hasPrevBitmap)
        {
            if (bitmap != null)
            {
                if (toPrevious)
                {
                    sbToRight.Begin();
                }
                else
                {
                    sbToLeft.Begin();
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
                if (toPrevious)
                {
                    sbFromLeft.Begin();
                }
                else
                {
                    sbFromRight.Begin();
                }
            }
            sbShow.Begin();
            await Task.Delay(SlideshowAnimationDuration);
        }
        semaphoreAnimation.Release();
    }

    private void PreviousBackground_Click(object sender, RoutedEventArgs e)
    {
        SetSelectedImageIndex(entry.SelectedImageIndex - 1, toPrevious: true);
    }

    private void NextBackground_Click(object sender, RoutedEventArgs e)
    {
        SetSelectedImageIndex(entry.SelectedImageIndex + 1);
    }

    private void Timer_Tick(object? sender, object e)
    {
        SetSelectedImageIndex(entry.SelectedImageIndex + 1);
    }

    private async Task AddFiles(IReadOnlyList<StorageFile> files)
    {
        foreach (var file in files)
        {
            OnProgress?.Invoke(this, $"Копирование: {file.Name}");
            await Settings.CopyFileWithConfirmation(XamlRoot, file, modsFolderPath);
            if (!entry.ModFiles.Contains(file.Name))
            {
                entry.ModFiles.Add(file.Name);
            }
        }
        OnProgress?.Invoke(this, null);
        RefillFileMenu();
    }

    private async Task AddImages(IReadOnlyList<StorageFile> files)
    {
        bool hasAddedImages = false;
        foreach (var file in files)
        {
            OnProgress?.Invoke(this, $"Копирование: {file.Name}");
            await Settings.CopyFileWithConfirmation(XamlRoot, file, imagesFolderPath);
            if (!entry.ModFiles.Contains(file.Name))
            {
                entry.ImageFiles.Add(file.Name);
            }
            hasAddedImages = true;
        }
        OnProgress?.Invoke(this, "");
        if (hasAddedImages)
        {
            SetSelectedImageIndex(entry.ImageFiles.Count - 1);
            SetSlideshow();
        }
    }

    private void OpenContainFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el)
        {
            if (el.DataContext is string filePath)
            {
                Process.Start("explorer.exe", "/select," + Path.GetFullPath(filePath, modsFolderPath));
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
                    RefillFileMenu();
                }
            }
        }
    }

    private async void RemoveBackground_Click(object sender, RoutedEventArgs e)
    {
        if (entry.SelectedImageIndex >= 0 && entry.SelectedImageIndex < entry.ImageFiles.Count)
        {
            var selectedImageIndex = entry.SelectedImageIndex;
            if (await AskDialog.ShowAsync(XamlRoot, "Удаление фона", $"Вы уверены, что хотите удалить текущий фон?", "Удалить", "Отмена"))
            {
                entry.ImageFiles.RemoveAt(selectedImageIndex);
                SetSelectedImageIndex(selectedImageIndex);
                SetSlideshow();
            }
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
                        if (Settings.SupportedModExtensions.Contains(ext) || Settings.SupportedImageExtensions.Contains(ext))
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
                        if (Settings.SupportedModExtensions.Contains(ext))
                        {
                            mods.Add(file);
                        }
                        else if (Settings.SupportedImageExtensions.Contains(ext))
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
        var slideshow = entry.Slideshow && entry.ImageFiles.Count > 1;
        btnSlideshowIcon.Glyph = slideshow ? "\uE769" : "\uE768";
        if (slideshow)
        {
            timerSlideshow.Start();
        }
        else
        {
            timerSlideshow.Stop();
        }
    }

    private void Slideshow_Click(object sender, RoutedEventArgs e)
    {
        entry.Slideshow = !entry.Slideshow;
        SetSlideshow();
    }
}
