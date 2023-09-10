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

        SetSelectedImageIndex(entry.SelectedImageIndex, direction: AnimationDirection.None);
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

    enum AnimationDirection
    {
        None, Previous, Next
    }

    private readonly SemaphoreSlim semaphoreAnimation = new(1, 1);

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
            var bitmap = await BitmapHelper.CreateBitmapFromFile(entry.ImageFiles[entry.SelectedImageIndex]);
            await semaphoreAnimation.WaitAsync();
            if (direction == AnimationDirection.Next)
            {
                sbToLeft.Begin();
                await Task.Delay(SlideshowAnimationDuration);
            }
            else if (direction == AnimationDirection.Previous)
            {
                sbToRight.Begin();
                await Task.Delay(SlideshowAnimationDuration);
            }
            imgBackground.Source = bitmap;
            if (direction == AnimationDirection.Next)
            {
                sbFromRight.Begin();
                await Task.Delay(SlideshowAnimationDuration);
            }
            else if (direction == AnimationDirection.Previous)
            {
                sbFromLeft.Begin();
                await Task.Delay(SlideshowAnimationDuration);
            }
            semaphoreAnimation.Release();
        }
        else
        {
            entry.SelectedImageIndex = 0;
            imgBackground.Source = null;
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
        SetSelectedImageIndex(entry.SelectedImageIndex + 1, direction: AnimationDirection.Next);
    }

    private async Task AddFiles(IEnumerable<StorageFile> files)
    {
        foreach (var file in files)
        {
            OnProgress?.Invoke(this, $"Копирование: {file.Name}");
            var targetPath = await Settings.CopyFileWithConfirmation(XamlRoot, file, modsFolderPath);
            if (!string.IsNullOrEmpty(targetPath))
            {
                var fileName = GetFileTitle(targetPath);
                var isInList = false;
                for (var i = 0; i  < entry.ModFiles.Count; i += 1)
                {
                    if (GetFileTitle(entry.ModFiles[i]) == fileName)
                    {
                        entry.ModFiles[i] = targetPath;
                        isInList = true;
                        break;
                    }
                }
                if (!isInList)
                {
                    entry.ModFiles.Add(targetPath);
                }
            }
        }
        OnProgress?.Invoke(this, null);
        RefillFileMenu();
    }

    private async Task AddImages(IEnumerable<StorageFile> files)
    {
        bool hasAddedImages = false;
        foreach (var file in files)
        {
            OnProgress?.Invoke(this, $"Копирование: {file.Name}");
            var targetPath = await Settings.CopyFileWithConfirmation(XamlRoot, file, imagesFolderPath);
            if (!string.IsNullOrEmpty(targetPath))
            {
                entry.ImageFiles.Add(targetPath);
                hasAddedImages = true;
            }
        }
        OnProgress?.Invoke(this, "");
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
                Process.Start("explorer.exe", "/select," + filePath);
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
                SetSelectedImageIndex(selectedImageIndex, direction: AnimationDirection.Next);
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
