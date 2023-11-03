﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DoomLauncher;

public enum AssetArch
{
    x64, x86, arm64, x64legacy, x86legacy, unknown, manual, notSelected
}

public partial class SettingsPageViewModel : ObservableObject
{
    public SettingsPageViewModel()
    {
#if IS_NON_PACKAGED
        var appName = "Неизвестное приложение";
        var appVersion = "Неизвестная версия";
        if (Assembly.GetEntryAssembly() is Assembly assembly)
        {
            if (assembly.GetCustomAttribute<AssemblyTitleAttribute>() is AssemblyTitleAttribute assemblyTitle) {
                appName = assemblyTitle.Title;
            }
            if (assembly.GetCustomAttribute<AssemblyFileVersionAttribute>() is AssemblyFileVersionAttribute assemblyVersion)
            {
                appVersion = assemblyVersion.Version.ToString();
            }
        }
#else
        var appName = Package.Current.DisplayName;
        var version = Package.Current.Id.Version;
        var appVersion = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
#endif
        AppVersion = $"{appName} {appVersion}";
        SteamGames = FileHelper.SteamAppIds.Select(item => new KeyValue(item.Key, item.Value.title)).ToList();
    }

    public List<KeyValue> SteamGames { get; }

    public KeyValue SteamGame {
        get => SteamGames.FirstOrDefault(steamGame => steamGame.Key == Settings.Current.SteamGame, SteamGames.First());
        set => SetProperty(Settings.Current.SteamGame, value.Key, Settings.Current, (settings, value) => settings.SteamGame = value);
    }

    public string AppVersion { get; }
}

public sealed partial class SettingsPage : Page
{

    public SettingsPageViewModel ViewModel = new();

    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        EventBus.ChangeBackground(this, null, AnimationDirection.None);
        EventBus.ChangeCaption(this, "Настройки");
        base.OnNavigatedTo(e);
    }

    [RelayCommand]
    private async Task AddRemoteDoomPackage()
    {
        EventBus.Progress(this, "Получение списка версий");
        var entries = await WebAPI.Current.GetGZDoomGitHubReleases();
        var onlinePackages = new List<GZDoomPackage>();
        foreach (var entry in entries)
        {
            foreach (var asset in entry.Assets)
            {
                if (asset.Name.EndsWith(".zip") && !asset.Name.Contains("macOS") && !asset.Name.Contains("ci_deps") && !asset.Name.Contains("AppImage"))
                {
                    var isLegacy = asset.Name.Contains("legacy");
                    AssetArch arch;
                    if (asset.Name.Contains("arm64"))
                    {
                        arch = AssetArch.arm64;
                    }
                    else if (!asset.Name.Contains("64bit") && !asset.Name.Contains("x64")) {
                        arch = isLegacy ? AssetArch.x86legacy : AssetArch.x86;
                    }
                    else
                    {
                        arch = isLegacy ? AssetArch.x64legacy : AssetArch.x64;
                    }
                    var version = FileHelper.ParseVersion(asset.Name);
                    if (!Settings.Current.GZDoomInstalls.Any(package => package.Version == version && package.Arch == arch))
                    {
                        var newAsset = new GZDoomPackage()
                        {
                            Path = asset.DownloadUrl,
                            Arch = arch,
                            Version = version,
                        };
                        onlinePackages.Add(newAsset);
                    }
                }
            }
        }
        EventBus.Progress(this, null);
        {
            var newAsset = await DialogHelper.ShowPackageSelectorAsync(onlinePackages.OrderByDescending(package => package.Version).ThenBy(package => package.Arch).ToList());
            if (newAsset != null)
            {
                await DownloadPackage(newAsset);
            }
        }
    }

    private async Task DownloadPackage(GZDoomPackage package)
    {
        if (!string.IsNullOrEmpty(package.Path))
        {
            EventBus.Progress(this, "Загрузка и извлечение архива");
            try
            {
                using var stream = await WebAPI.Current.DownloadUrl(package.Path);
                using var zipArchive = new ZipArchive(stream, ZipArchiveMode.Read);
                var folderName = FileHelper.PackageToFolderName(package);
                var targetPath = Path.Combine(FileHelper.PackagesFolderPath, folderName);
                await Task.Run(
                    () => zipArchive.ExtractToDirectory(targetPath, overwriteFiles: true)
                );
                var newPackage = new GZDoomPackage()
                {
                    Path = Path.Combine(folderName, "gzdoom.exe"),
                    Version = package.Version,
                    Arch = package.Arch,
                };
                Settings.Current.GZDoomInstalls.Insert(0, newPackage);
            }
            finally
            {
                EventBus.Progress(this, null);
            }
        }
    }

    [RelayCommand]
    private async Task AddLocalDoomPackage()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();

        // Need to initialize the picker object with the hwnd / IInitializeWithWindow 
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinApi.HWND);

        // Now we can use the picker object as normal
        picker.FileTypeFilter.Add(".exe");
        picker.CommitButtonText = "Выбрать GZDoom";
        var file = await picker.PickSingleFileAsync();
        if (file != null && FileHelper.ValidateGZDoomPath(file.Path))
        {
            var version = FileHelper.GetFileVersion(file.Path) is string s ? FileHelper.ParseVersion(s) : null;
            if (Settings.Current.GZDoomInstalls.FirstOrDefault(package => package.Path == file.Path) is GZDoomPackage package)
            {
                package.Version = version;
            }
            else
            {
                var newPackage = new GZDoomPackage
                {
                    Path = file.Path,
                    Version = version,
                    Arch = AssetArch.manual,
                };
                Settings.Current.GZDoomInstalls.Insert(0, newPackage);
            }
        }
    }

    [RelayCommand]
    private async Task AddLocalIWad()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();

        // Need to initialize the picker object with the hwnd / IInitializeWithWindow 
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinApi.HWND);

        // Now we can use the picker object as normal
        picker.FileTypeFilter.Add(".wad");
        picker.CommitButtonText = "Выбрать IWad";
        var files = await picker.PickMultipleFilesAsync();

        foreach (var file in files)
        {
            EventBus.Progress(this, $"Копирование: {file.Name}");
            await FileHelper.CopyFileWithConfirmation(file, FileHelper.IWadFolderPath);
            if (!Settings.Current.IWadFiles.Contains(file.Name))
            {
                Settings.Current.IWadFiles.Insert(0, file.Name);
            }
        }
        EventBus.Progress(this, null);
    }

    private void ToggleDefaultGZDoom_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el)
        {
            if (el.DataContext is GZDoomPackage package)
            {
                Settings.Current.DefaultGZDoomPath = Settings.Current.DefaultGZDoomPath == package.Path ? "" : package.Path;
            }
        }
    }

    private void OpenContainFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el)
        {
            if (el.DataContext is GZDoomPackage package)
            {
                Process.Start("explorer.exe", "/select," + Path.GetFullPath(package.Path, FileHelper.PackagesFolderPath));
            }
        }
    }

    private async void RemovePackage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el)
        {
            if (el.DataContext is GZDoomPackage package)
            {
                var title = FileHelper.GZDoomPackageToTitle(package);
                if (await DialogHelper.ShowAskAsync("Удаление ссылки", $"Вы уверены, что хотите удалить ссылку на '{title}'?", "Удалить", "Отмена"))
                {
                    Settings.Current.GZDoomInstalls.Remove(package);
                }
            }
        }
    }

    private void ToggleDefaultIWad_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el)
        {
            if (el.DataContext is string iWadFile)
            {
                Settings.Current.DefaultIWadFile = Settings.Current.DefaultIWadFile == iWadFile ? "" : iWadFile;
            }
        }
    }

    private void OpenIWadFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el)
        {
            if (el.DataContext is string iWadFile)
            {
                Process.Start("explorer.exe", "/select," + Path.GetFullPath(iWadFile, FileHelper.IWadFolderPath));
            }
        }
    }

    private async void RemoveIWad_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el)
        {
            if (el.DataContext is string iWadFile)
            {
                var title = FileHelper.IWadFileToTitle(iWadFile);
                if (await DialogHelper.ShowAskAsync("Удаление ссылки", $"Вы уверены, что хотите удалить ссылку на '{title}'?", "Удалить", "Отмена"))
                {
                    Settings.Current.IWadFiles.Remove(iWadFile);
                }
            }
        }
    }
}
