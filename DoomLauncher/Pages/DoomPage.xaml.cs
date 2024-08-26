using DoomLauncher.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public DoomPageViewModel ViewModel { get; set; } = new(SettingsViewModel.Current);

    public DoomPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is DoomEntryViewModel entry)
        {
            ViewModel.LoadEntry(entry);
        }
        base.OnNavigatedTo(e);
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        EventBus.OnRightDragEnter += Root_DragEnter;
        EventBus.OnRightDragOver += DropHelper_DragOver;
        EventBus.OnRightDrop += DropHelper_Drop;
        ViewModel.SlideshowTimer.Interval = ViewModel.SlideshowInterval;
        ViewModel.SlideshowTimer.Tick += ViewModel.Timer_Tick;
        ViewModel.ModFileList.CollectionChanged += ModFileList_CollectionChanged;
        ViewModel.ImageFileList.CollectionChanged += ImageFileList_CollectionChanged;  
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        EventBus.OnRightDragEnter -= Root_DragEnter;
        EventBus.OnRightDragOver -= DropHelper_DragOver;
        EventBus.OnRightDrop -= DropHelper_Drop;
        ViewModel.SlideshowTimer.Tick -= ViewModel.Timer_Tick;
        ViewModel.SlideshowTimer.Stop();
        ViewModel.ModFileList.CollectionChanged -= ModFileList_CollectionChanged;
        ViewModel.ImageFileList.CollectionChanged -= ImageFileList_CollectionChanged;
    }

    private void ModFileList_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        ViewModel.Entry.ModFiles = ViewModel.ModFileList.Select(modFile => modFile.Path).ToList();
    }

    private void ImageFileList_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        ViewModel.Entry.ImageFiles = ViewModel.ImageFileList.Select(imageFile => imageFile.Path).ToList();
    }

    private void MenuFlyout_Opening(object sender, object e)
    {
        if (sender is MenuFlyout menu)
        {
            menu.Items.Clear();
            var browseItem = new MenuFlyoutItem()
            {
                Text = Strings.Resources.ChooseLocal,
                Icon = new FontIcon()
                {
                    Glyph = "\uEC50",
                    FontSize = 14,
                },
                Command = ViewModel.AddLocalModFileCommand,
            };
            menu.Items.Add(browseItem);
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(new MenuFlyoutItem()
            {
                Text = Strings.Resources.Favorites,
                IsEnabled = false,
                Icon = new FontIcon()
                {
                    Glyph = "\uE735",
                    FontSize = 14,
                }
            });
            if (ViewModel.FavoriteFiles.Count > 0)
            {
                foreach (var filePath in ViewModel.FavoriteFiles)
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
                            await ViewModel.AddModFiles([file]);
                        }
                    };
                    menu.Items.Add(item);
                }
            }
            else
            {
                menu.Items.Add(new MenuFlyoutItem()
                {
                    Text = Strings.Resources.Empty,
                    IsEnabled = false,
                });
            }
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
                    await ViewModel.AddModFiles(mods);
                }
                if (images.Count > 0)
                {
                    await ViewModel.AddImages(images);
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }
    }

    private void ListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ImageFileViewModel imageFile)
        {
            var ind = ViewModel.ImageFileList.IndexOf(imageFile);
            if (ind != ViewModel.Entry.SelectedImageIndex && ind > -1)
            {
                ViewModel.SetSelectedImageIndex(ind, ind > ViewModel.Entry.SelectedImageIndex ? AnimationDirection.Next : AnimationDirection.Previous);
            }
        }
    }
}
