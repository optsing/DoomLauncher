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
using static System.Net.Mime.MediaTypeNames;
using static System.Net.WebRequestMethods;
using static System.Runtime.InteropServices.JavaScript.JSType;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DoomLauncher;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class DoomPage : Page
{
    private readonly DoomEntry entry;
    private readonly IntPtr hWnd;
    private readonly string modsFolderPath;
    private readonly string imagesFolderPath;
    private readonly Settings settings;

    public DoomPage(DoomEntry entry, IntPtr hWnd, string dataFolderPath, Settings settings)
    {
        InitializeComponent();
        this.entry = entry;
        this.hWnd = hWnd;
        this.modsFolderPath = Path.Combine(dataFolderPath, "mods");
        this.imagesFolderPath = Path.Combine(dataFolderPath, "images");
        this.settings = settings;

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

    private void NextBackground_Click(object sender, RoutedEventArgs e)
    {
        if (entry.SelectedImageIndex < entry.ImageFiles.Count - 1)
        {
            entry.SelectedImageIndex += 1;
        } else
        {
            entry.SelectedImageIndex = 0;
        }
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
            entry.SelectedImageIndex = entry.ImageFiles.Count - 1;
        }
    }

    public static BitmapImage? GetCurrentBackground(IList<string> list, int selectedImageIndex)
    {
        if (list.Any() && selectedImageIndex < list.Count)
        {
            return new BitmapImage(new Uri(list[selectedImageIndex]));
        }
        return null;
    }

    public static bool HasMoreItems(int itemsCount, int count)
    {
        return itemsCount > count;
    }

    public static Visibility HasNoItems(int itemsCount)
    {
        return itemsCount == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void LwModFiles_DragOver(object sender, DragEventArgs e)
    {
        var deferral = e.GetDeferral();
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
                        e.AcceptedOperation = DataPackageOperation.Copy;
                        break;
                    }
                }
            }
        }
        deferral.Complete();
    }

    private async void LwModFiles_Drop(object sender, DragEventArgs e)
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
        if (entry.SelectedImageIndex < entry.ImageFiles.Count)
        {
            if (await AskDialog.ShowAsync(XamlRoot, "Удаление фона", $"Вы уверены, что хотите удалить текущий фон?", "Удалить", "Отмена"))
            {
                entry.ImageFiles.RemoveAt(entry.SelectedImageIndex);
            }
        }
    }

    public static string GetFileTitle(string filePath)
    {
        return Path.GetFileName(filePath);
    }
}
