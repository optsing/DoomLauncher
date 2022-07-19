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
    public DoomPage(DoomEntry entry, IntPtr hwnd)
    {
        InitializeComponent();
        Entry = entry;
        HWND = hwnd;
        Entry.PropertyChanged += Entry_PropertyChanged;
        Entry.ModFiles.CollectionChanged += ModFiles_CollectionChanged;
    }

    private void ModFiles_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnSave?.Invoke(this, new());
    }

    private void Entry_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnSave?.Invoke(this, new());
    }

    private DoomEntry Entry { get; set; }
    private IntPtr HWND
    {
        get; set;
    }
    public event EventHandler<DoomEntry> OnStart;
    public event EventHandler OnSave;
 

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
        AddFiles(files);
    }

    private void AddFiles(IEnumerable<IStorageItem> files)
    {
        foreach (var file in files)
        {
            var path = file.Path;
            if (!Entry.ModFiles.Any(item => item.Path == path))
            {
                Entry.ModFiles.Add(new(path));
            }
        }
    } 


    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        while (lwModFiles.SelectedItems.Count > 0)
        {
            Entry.ModFiles.Remove(lwModFiles.SelectedItems[0] as NamePath);
        }
    }

    public static Visibility HasNoModFiles(int count)
    {
        return count > 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private readonly string[] SupportedFileExtensions = new[] { ".pk3", ".wad" };

    private async Task<IStorageItem[]> GetDraggedFiles(DataPackageView data)
    {
        if (data.Contains(StandardDataFormats.StorageItems))
        {
            var items = await data.GetStorageItemsAsync();
            if (items.Count > 0)
            {
                return items.Where(item => {
                    var ext = Path.GetExtension(item.Name).ToLowerInvariant();
                    return SupportedFileExtensions.Contains(ext);
                }).ToArray();
            }
        }
        return Array.Empty<IStorageItem>();
    } 

    private async void LwModFiles_DragOver(object sender, DragEventArgs e)
    {
        var deferral = e.GetDeferral();
        var files = await GetDraggedFiles(e.DataView);
        if (files.Length > 0)
        {
            e.AcceptedOperation = DataPackageOperation.Link;
        }
        deferral.Complete();
    }

    private async void LwModFiles_Drop(object sender, DragEventArgs e)
    {
        var deferral = e.GetDeferral();
        var files = await GetDraggedFiles(e.DataView);
        if (files.Length > 0)
        {
            AddFiles(files);
        }
        deferral.Complete();
    }
}
