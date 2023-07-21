using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
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
                .Where(file => !entry.ModFiles.Any(modFile => modFile.Path == file.Path));

            if (files.Any())
            { 
                mfAppendFile.Items.Add(new MenuFlyoutSeparator());
                foreach (var file in files)
                {
                    var fileItem = new NamePath(file.Path);
                    var item = new MenuFlyoutItem()
                    {
                        Text = fileItem.Name,
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

    public event EventHandler<DoomEntry> OnStart;
    public event EventHandler<DoomEntry> OnEdit;
    public event EventHandler<DoomEntry> OnRemove;
    public event EventHandler<bool> OnProgress;

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        OnStart?.Invoke(this, entry);
    }

    private void EditMod_Click(object sender, RoutedEventArgs e)
    {
        OnEdit?.Invoke(this, entry);
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
        foreach (var fileExtension in MainWindow.SupportedModExtensions)
        {
            picker.FileTypeFilter.Add(fileExtension);
        }

        var files = await picker.PickMultipleFilesAsync();
        if (files.Any())
        {
            await AddFiles(files);
        }
    }

    private async void Background_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();

        // Need to initialize the picker object with the hwnd / IInitializeWithWindow
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

        // Now we can use the picker object as normal
        foreach (var fileExtension in MainWindow.SupportedImageExtensions)
        {
            picker.FileTypeFilter.Add(fileExtension);
        }

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            await SetImage(file);
        }
    }

    private async Task<bool> ShowAskDialog(string title, string text, string primaryButton, string closeButton)
    {
        var dialog = new AskDialog(XamlRoot, title, text, primaryButton, closeButton);
        return ContentDialogResult.Primary == await dialog.ShowAsync();
    }

    private async Task<string> CopyFileWithConfirmation(StorageFile file, string targetFolder)
    {
        var targetPath = Path.Combine(targetFolder, file.Name);
        if (targetPath != file.Path)
        {
            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }
            if (!File.Exists(targetPath) || await ShowAskDialog(
                "Добавление с заменой",
                $"Файл '{file.Name}' уже существует в папке лаунчера.\nЗаменить?",
                "Заменить",
                "Не заменять"
            ))
            {
                OnProgress?.Invoke(this, true);
                using var sourceStream = await file.OpenStreamForReadAsync();
                using var destinationStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write);
                await sourceStream.CopyToAsync(destinationStream);
                OnProgress?.Invoke(this, false);
            }
        }
        return targetPath;
    }

    private async Task AddFiles(IEnumerable<StorageFile> files)
    {
        foreach (var file in files)
        {
            var targetPath = await CopyFileWithConfirmation(file, modsFolderPath);
            if (!string.IsNullOrEmpty(targetPath))
            {
                var newModFile = new NamePath(targetPath);
                var existModFile = entry.ModFiles.FirstOrDefault(item => item.Name == newModFile.Name);
                if (existModFile == null)
                {
                    entry.ModFiles.Add(newModFile);
                }
                else
                {
                    existModFile.Path = newModFile.Path;
                }
            }
        }
        RefillFileMenu();
    }

    private async Task SetImage(StorageFile file)
    {
        var targetPath = await CopyFileWithConfirmation(file, imagesFolderPath);
        if (!string.IsNullOrEmpty(targetPath))
        {
            entry.ImageFiles.Clear();
            entry.ImageFiles.Add(targetPath);
        }
    }

    public static BitmapImage GetCurrentBackground(IEnumerable<string> list)
    {
        if (list.Any())
        {
            return new BitmapImage(new Uri(list.First()));
        }
        //var path = Path.GetFullPath("Assets/DefaultCover.jpg");
        //return new BitmapImage(new Uri(path));
        return null;
    }

    public static bool HasItems(int itemsCount)
    {
        return itemsCount > 0;
    }

    public static Visibility HasNoItems(int itemsCount)
    {
        return itemsCount == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static async Task<(List<StorageFile>, List<StorageFile>)> GetDraggedFiles(DataPackageView data)
    {
        var modResult = new List<StorageFile>();
        var imageResult = new List<StorageFile>();
        if (data.Contains(StandardDataFormats.StorageItems))
        {
            var items = await data.GetStorageItemsAsync();
            foreach (var item in items)
            {
                if (item is StorageFile file)
                {
                    var ext = Path.GetExtension(file.Name).ToLowerInvariant();
                    if (MainWindow.SupportedModExtensions.Contains(ext))
                    {
                        modResult.Add(file);
                    }
                    else if (MainWindow.SupportedImageExtensions.Contains(ext))
                    {
                        imageResult.Add(file);
                    }
                }
            }
        }
        return (modResult, imageResult);
    }

    private async void LwModFiles_DragOver(object sender, DragEventArgs e)
    {
        var deferral = e.GetDeferral();
        var (files, images) = await GetDraggedFiles(e.DataView);
        if (files.Count + images.Count > 0)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
        deferral.Complete();
    }

    private async void LwModFiles_Drop(object sender, DragEventArgs e)
    {
        var (files, images) = await GetDraggedFiles(e.DataView);
        if (files.Count > 0)
        {
            await AddFiles(files);
        }
        if (images.Count > 0)
        {
            await SetImage(images[0]);
        }
    }

    private void OpenContainFolder_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var file = button.DataContext as NamePath;
        Process.Start("explorer.exe", "/select," + file.Path);
    }

    private async void RemoveFile_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var file = button.DataContext as NamePath;
        if (await ShowAskDialog("Удаление ссылки на файл", $"Вы уверены, что хотите удалить ссылку на файл '{file.Name}'?", "Удалить", "Отмена"))
        {
            entry.ModFiles.Remove(file);
            RefillFileMenu();
        }
    }

    private void RemoveBackground_Click(object sender, RoutedEventArgs e)
    {
        entry.ImageFiles.Clear();
    }
}
