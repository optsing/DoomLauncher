using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DoomLauncher.ViewModels;

public partial class DoomPageViewModel(DispatcherTimer timer) : ObservableObject
{
    public DoomEntryViewModel Entry = new();

    private readonly DispatcherTimer timerSlideshow = timer;

    [ObservableProperty]
    private int currentTicksToSlideshow;

    [ObservableProperty]
    private bool isSlideshowEnabled;

    partial void OnIsSlideshowEnabledChanged(bool value)
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

    [RelayCommand]
    private static void ToggleFavoriteFile(string filePath)
    {
        if (!SettingsViewModel.Current.FavoriteFiles.Remove(filePath))
        {
            SettingsViewModel.Current.FavoriteFiles.Add(filePath);
        }
    }

    [RelayCommand]
    private static void OpenContainingFolder(string filePath)
    {
        Process.Start("explorer.exe", "/select," + Path.GetFullPath(filePath, FileHelper.ModsFolderPath));
    }

    [RelayCommand]
    private async Task RemoveFile(string filePath)
    {
        var fileName = FileHelper.GetFileTitle(filePath);
        if (await DialogHelper.ShowAskAsync("Удаление ссылки на файл", $"Вы уверены, что хотите удалить ссылку на файл '{fileName}'?", "Удалить", "Отмена"))
        {
            Entry.ModFiles.Remove(filePath);
        }
    }
}
