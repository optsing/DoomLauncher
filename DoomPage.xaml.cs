using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
    private readonly bool IS_COPY_TO_INNER_FOLDER = false;

    public DoomPage(DoomEntry entry, IntPtr hwnd, string dataFolderPath)
    {
        InitializeComponent();
        Entry = entry;
        HWND = hwnd;
        WadsFolderPath = Path.Combine(dataFolderPath, entry.Id);
    }

    private string WadsFolderPath
    {
        get; set;
    }
    private DoomEntry Entry
    {
        get; set;
    }
    private IntPtr HWND
    {
        get; set;
    }
    public event EventHandler<DoomEntry> OnStart;

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        OnStart?.Invoke(this, Entry);
    }

    private async void Append_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();

        // Need to initialize the picker object with the hwnd / IInitializeWithWindow 
        WinRT.Interop.InitializeWithWindow.Initialize(picker, HWND);

        // Now we can use the picker object as normal
        foreach (var fileExtension in SupportedFileExtensions)
        {
            picker.FileTypeFilter.Add(fileExtension);
        }

        var files = await picker.PickMultipleFilesAsync();
        AddFiles(files.Select(file => file.Path));
    }

    private void AddFiles(IEnumerable<string> filePathes)
    {
        if (IS_COPY_TO_INNER_FOLDER)
        {
            if (!Directory.Exists(WadsFolderPath))
            {
                Directory.CreateDirectory(WadsFolderPath);
            }
        }
        foreach (var path in filePathes)
        {
            var targetPath = path;
            if (IS_COPY_TO_INNER_FOLDER)
            {
                targetPath = Path.Combine(WadsFolderPath, Path.GetFileName(path));
                File.Copy(path, targetPath, true);
            }
            var modFile = new NamePath(targetPath);
            if (!Entry.ModFiles.Any(item => item.Name == modFile.Name))
            {
                Entry.ModFiles.Add(modFile);
            }
        }
    }


    public static Visibility HasNoModFiles(int count)
    {
        return count > 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private readonly string[] SupportedFileExtensions = new[] { ".pk3", ".wad" };

    private async Task<List<string>> GetDraggedFiles(DataPackageView data)
    {
        var result = new List<string>();
        if (data.Contains(StandardDataFormats.StorageItems))
        {
            var items = await data.GetStorageItemsAsync();
            foreach (var item in items)
            {
                if (item is StorageFile file)
                {
                    var ext = Path.GetExtension(file.Name).ToLowerInvariant();
                    if (SupportedFileExtensions.Contains(ext))
                    {
                        result.Add(file.Path);
                    }
                }
            }
        }
        return result;
    }

    private async void LwModFiles_DragOver(object sender, DragEventArgs e)
    {
        var deferral = e.GetDeferral();
        var files = await GetDraggedFiles(e.DataView);
        if (files.Count > 0)
        {
            e.AcceptedOperation = DataPackageOperation.Link;
        }
        deferral.Complete();
    }

    private async void LwModFiles_Drop(object sender, DragEventArgs e)
    {
        var deferral = e.GetDeferral();
        var files = await GetDraggedFiles(e.DataView);
        if (files.Count > 0)
        {
            AddFiles(files);
        }
        deferral.Complete();
    }

    private void RemoveFile_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var file = button.DataContext as NamePath;
        Entry.ModFiles.Remove(file);
    }
}
