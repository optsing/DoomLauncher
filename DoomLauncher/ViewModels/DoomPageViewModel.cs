using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace DoomLauncher.ViewModels;

public partial class ModFileViewModel(string filePath, bool isInFavorites) : ObservableObject
{
    public string Path => filePath;
    public string Title => FileHelper.GetFileTitle(filePath);

    [ObservableProperty]
    private bool isInFavorites = isInFavorites;
}

public partial class ImageFileViewModel : ObservableObject
{
    public string Path { get; }

    public string FullPath => System.IO.Path.GetFullPath(Path, FileHelper.ImagesFolderPath);

    [ObservableProperty]
    private BitmapImage? image = null;

    public ImageFileViewModel(string imagePath)
    {
        Path = imagePath;
        LoadBitmap();
    }

    private async void LoadBitmap()
    {
        Image = await BitmapHelper.CreateBitmapFromFile(FullPath, isPreview: true);
    }
}

public partial class DoomPageViewModel(SettingsViewModel settings) : ObservableObject
{
    public DoomEntryViewModel Entry = new();

    public readonly DispatcherTimer SlideshowTimer = new();
    public readonly TimeSpan SlideshowInterval = TimeSpan.FromSeconds(1);
    public readonly int TicksToSlideshow = 10;

    public readonly string DefaultGZDoomPath = settings.DefaultGZDoomPath;
    public readonly string DefaultIWadFile = settings.DefaultIWadFile;
    public readonly ObservableCollection<string> FavoriteFiles = settings.FavoriteFiles;

    public ObservableCollection<ModFileViewModel> ModFileList { get; } = [];
    public ObservableCollection<ImageFileViewModel> ImageFileList { get; } = [];

    [ObservableProperty]
    private int currentTicksToSlideshow;

    [ObservableProperty]
    private bool isSlideshowEnabled;

    partial void OnIsSlideshowEnabledChanged(bool value)
    {
        if (value)
        {
            SlideshowTimer.Start();
        }
        else
        {
            SlideshowTimer.Stop();
        }
    }

    public void Timer_Tick(object? sender, object e)
    {
        if (CurrentTicksToSlideshow < TicksToSlideshow)
        {
            CurrentTicksToSlideshow += 1;
        }
        else
        {
            SetSelectedImageIndex(Entry.SelectedImageIndex + 1, direction: AnimationDirection.Next);
            CurrentTicksToSlideshow = 0;
        }
    }

    public void LoadEntry(DoomEntryViewModel entry)
    {
        Entry = entry;
        ModFileList.Clear();
        foreach (var filePath in entry.ModFiles)
        {
            ModFileList.Add(new(filePath, FavoriteFiles.Contains(filePath)));
        }
        ImageFileList.Clear();
        foreach (var imagePath in entry.ImageFiles)
        {
            ImageFileList.Add(new(imagePath));
        }
        SetSlideshow();
        SetSelectedImageIndex(entry.SelectedImageIndex, direction: AnimationDirection.None);
        EventBus.ChangeCaption(this, entry.Name);
    }

    [RelayCommand]
    private void ToggleFavoriteFile(ModFileViewModel modFile)
    {
        if (FavoriteFiles.Remove(modFile.Path))
        {
            modFile.IsInFavorites = false;
        }
        else
        {
            FavoriteFiles.Add(modFile.Path);
            modFile.IsInFavorites = true;
        }
    }

    [RelayCommand]
    private static void OpenContainingFolder(ModFileViewModel modFile)
    {
        Process.Start("explorer.exe", "/select," + Path.GetFullPath(modFile.Path, FileHelper.ModsFolderPath));
    }

    [RelayCommand]
    private async Task RemoveModFile(ModFileViewModel modFile)
    {
        if (await DialogHelper.ShowAskAsync("Удаление ссылки на файл", $"Вы уверены, что хотите удалить ссылку на файл '{modFile.Title}'?", "Удалить", "Отмена"))
        {
            ModFileList.Remove(modFile);
        }
    }

    public async Task AddModFiles(IReadOnlyList<StorageFile> files)
    {
        foreach (var file in files)
        {
            EventBus.Progress(this, $"Копирование: {file.Name}");
            await FileHelper.CopyFileWithConfirmation(file, FileHelper.ModsFolderPath);
            if (!ModFileList.Any(modFile => modFile.Path == file.Name))
            {
                ModFileList.Add(new(file.Name, FavoriteFiles.Contains(file.Name)));
            }
        }
        EventBus.Progress(this, null);
    }

    [RelayCommand]
    private async Task AddLocalModFile()
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
        if (files.Count > 0)
        {
            await AddModFiles(files);
        }
    }

    public async Task AddImages(IReadOnlyList<StorageFile> files)
    {
        bool hasAddedImages = false;
        foreach (var file in files)
        {
            EventBus.Progress(this, $"Копирование: {file.Name}");
            await FileHelper.CopyFileWithConfirmation(file, FileHelper.ImagesFolderPath);
            if (!ImageFileList.Any(imageFile => imageFile.Path == file.Name))
            {
                ImageFileList.Add(new(file.Name));
                hasAddedImages = true;
            }
        }
        EventBus.Progress(this, null);
        if (hasAddedImages)
        {
            SetSelectedImageIndex(ImageFileList.Count - 1, direction: AnimationDirection.Next);
            SetSlideshow();
        }
    }

    [RelayCommand]
    private async Task AddBackground()
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
        if (files.Count > 0)
        {
            await AddImages(files);
        }
    }

    [RelayCommand]
    private static void OpenImageContainingFolder(ImageFileViewModel imageFile)
    {
        Process.Start("explorer.exe", "/select," + imageFile.FullPath);
    }

    [RelayCommand]
    private async Task RemoveImageFile(ImageFileViewModel imageFile)
    {
        IsSlideshowEnabled = false;
        if (await DialogHelper.ShowAskAsync("Удаление фона", $"Вы уверены, что хотите удалить выбранный фон?", "Удалить", "Отмена"))
        {
            var selectedImageIndex = Entry.SelectedImageIndex;
            ImageFileList.Remove(imageFile);
            SetSelectedImageIndex(selectedImageIndex, direction: AnimationDirection.Next);
        }
        SetSlideshow();
    }

    [RelayCommand]
    private void GoToPreviousBackground()
    {
        SetSelectedImageIndex(Entry.SelectedImageIndex - 1, direction: AnimationDirection.Previous);
    }

    [RelayCommand]
    private void GoToNextBackground()
    {
        SetSelectedImageIndex(Entry.SelectedImageIndex + 1, direction: AnimationDirection.Next);
    }

    public void SetSelectedImageIndex(int ind, AnimationDirection direction)
    {
        if (ImageFileList.Count > 0)
        {
            if (ind < 0)
            {
                ind = ImageFileList.Count - 1;
            }
            else if (ind >= ImageFileList.Count)
            {
                ind = 0;
            }
            Entry.SelectedImageIndex = ind;
            EventBus.ChangeBackground(this, ImageFileList[Entry.SelectedImageIndex].FullPath, direction);
        }
        else
        {
            Entry.SelectedImageIndex = 0;
            EventBus.ChangeBackground(this, null, direction);
        }
    }

    private void SetSlideshow()
    {
        IsSlideshowEnabled = Entry.Slideshow && ImageFileList.Count > 1;
    }

    [RelayCommand]
    private void ToggleSlideshow()
    {
        Entry.Slideshow = !Entry.Slideshow;
        CurrentTicksToSlideshow = 0;
        SetSlideshow();
    }
}
