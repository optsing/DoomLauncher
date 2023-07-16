using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    private readonly bool copyFilesToLauncherFolder;

    public DoomPage(DoomEntry entry, IntPtr hWnd, string dataFolderPath, bool copyFilesToLauncherFolder)
    {
        InitializeComponent();
        this.entry = entry;
        this.hWnd = hWnd;
        this.modsFolderPath = Path.Combine(dataFolderPath, "mods");
        this.imagesFolderPath = Path.Combine(dataFolderPath, "images");
        this.copyFilesToLauncherFolder = copyFilesToLauncherFolder;
    }

    public event EventHandler<DoomEntry> OnStart;
    public event EventHandler<DoomEntry> OnEdit;
    public event EventHandler<DoomEntry> OnRemove;

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        OnStart?.Invoke(this, entry);
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
        await AddFiles(files.Select(file => file.Path));
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
            await SetImage(file.Path);
        }
    }

    private async Task<bool> ShowAskDialog(string text, string primaryButton)
    {
        var dialog = new AskDialog(XamlRoot, text, primaryButton);
        return ContentDialogResult.Primary == await dialog.ShowAsync();
    }

    private async Task<string> CopyFileWithConfirmation(string originalPath, string targetFolder)
    {
        if (!Directory.Exists(targetFolder))
        {
            Directory.CreateDirectory(targetFolder);
        }
        var fileName = Path.GetFileName(originalPath);
        var targetPath = Path.Combine(targetFolder, fileName);
        if (!File.Exists(targetPath) || await ShowAskDialog($"Файл '{fileName}' существует в папке лаунчера.\nЗаменить?", "Заменить"))
        {
            File.Copy(originalPath, targetPath, true);
        }
        return targetPath;
    }

    private async Task AddFiles(IEnumerable<string> filePathes)
    {
        foreach (var path in filePathes)
        {
            var targetPath = path;
            if (copyFilesToLauncherFolder)
            {
                targetPath = await CopyFileWithConfirmation(path, modsFolderPath);
            }
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

    private async Task SetImage(string imagePath)
    {
        if (copyFilesToLauncherFolder)
        {
            await CopyFileWithConfirmation(imagePath, imagesFolderPath);
        }
        entry.ImageFiles.Clear();
        entry.ImageFiles.Add(imagePath);
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

    private static async Task<(List<string>, List<string>)> GetDraggedFiles(DataPackageView data)
    {
        var modResult = new List<string>();
        var imageResult = new List<string>();
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
                        modResult.Add(file.Path);
                    }
                    else if (MainWindow.SupportedImageExtensions.Contains(ext))
                    {
                        imageResult.Add(file.Path);
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
            e.AcceptedOperation = DataPackageOperation.Link;
        }
        deferral.Complete();
    }

    private async void LwModFiles_Drop(object sender, DragEventArgs e)
    {
        var deferral = e.GetDeferral();
        var (files, images) = await GetDraggedFiles(e.DataView);
        if (files.Count > 0)
        {
            await AddFiles(files);
        }
        if (images.Count > 0)
        {
            await SetImage(images[0]);
        }
        deferral.Complete();
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
        if (await ShowAskDialog($"Вы уверены, что хотите удалить ссылку на файл '{file.Name}'?", "Удалить"))
        {
            entry.ModFiles.Remove(file);
        }
    }

    private void EditMod_Click(object sender, RoutedEventArgs e)
    {
        OnEdit?.Invoke(this, entry);
    }

    private void RemoveMod_Click(object sender, RoutedEventArgs e)
    {
        OnRemove?.Invoke(this, entry);
    }

    private void RemoveBackground_Click(object sender, RoutedEventArgs e)
    {
        entry.ImageFiles.Clear();
    }
}
