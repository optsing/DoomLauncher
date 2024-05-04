using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace DoomLauncher.ViewModels;

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

    public KeyValue SteamGame
    {
        get => SteamGames.FirstOrDefault(steamGame => steamGame.Key == SettingsViewModel.Current.SteamGame, SteamGames.First());
        set => SetProperty(SettingsViewModel.Current.SteamGame, value.Key, SettingsViewModel.Current, (settings, value) => settings.SteamGame = value);
    }

    public string AppVersion { get; }

    [RelayCommand]
    private async Task AddRemoteDoomPackage()
    {
        EventBus.Progress(this, "Получение списка версий");
        var entries = await WebAPI.Current.GetGZDoomGitHubReleases();
        var onlinePackages = new List<DoomPackageViewModel>();
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
                    else if (!asset.Name.Contains("64bit") && !asset.Name.Contains("x64"))
                    {
                        arch = isLegacy ? AssetArch.x86legacy : AssetArch.x86;
                    }
                    else
                    {
                        arch = isLegacy ? AssetArch.x64legacy : AssetArch.x64;
                    }
                    var version = FileHelper.ParseVersion(asset.Name);
                    if (!SettingsViewModel.Current.GZDoomInstalls.Any(package => package.Version == version && package.Arch == arch))
                    {
                        var newAsset = new DoomPackageViewModel()
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
            var newAsset = await DialogHelper.ShowPackageSelectorAsync([.. onlinePackages.OrderByDescending(package => package.Version).ThenBy(package => package.Arch)]);
            if (newAsset != null)
            {
                await DownloadPackage(newAsset);
            }
        }
    }

    private async Task DownloadPackage(DoomPackageViewModel package)
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
                var newPackage = new DoomPackageViewModel()
                {
                    Path = Path.Combine(folderName, "gzdoom.exe"),
                    Version = package.Version,
                    Arch = package.Arch,
                };
                SettingsViewModel.Current.GZDoomInstalls.Add(newPackage);
            }
            finally
            {
                EventBus.Progress(this, null);
            }
        }
    }

    [RelayCommand]
    private static async Task AddLocalDoomPackage()
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
            if (SettingsViewModel.Current.GZDoomInstalls.FirstOrDefault(package => package.Path == file.Path) is DoomPackageViewModel package)
            {
                package.Version = version;
            }
            else
            {
                var newPackage = new DoomPackageViewModel
                {
                    Path = file.Path,
                    Version = version,
                    Arch = AssetArch.manual,
                };
                SettingsViewModel.Current.GZDoomInstalls.Add(newPackage);
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
            if (!SettingsViewModel.Current.IWadFiles.Contains(file.Name))
            {
                SettingsViewModel.Current.IWadFiles.Add(file.Name);
            }
        }
        EventBus.Progress(this, null);
    }

    [RelayCommand]
    private static void ToggleDefaultDoomPackage(DoomPackageViewModel? package)
    {
        if (package == null)
        {
            return;
        }
        SettingsViewModel.Current.DefaultGZDoomPath = SettingsViewModel.Current.DefaultGZDoomPath == package.Path ? "" : package.Path;
    }

    [RelayCommand]
    private static void OpenFolderDoomPackage(DoomPackageViewModel? package)
    {
        if (package == null)
        {
            return;
        }
        Process.Start("explorer.exe", "/select," + Path.GetFullPath(package.Path, FileHelper.PackagesFolderPath));
    }

    [RelayCommand]
    private static async Task RemoveDoomPackage(DoomPackageViewModel? package)
    {
        if (package == null)
        {
            return;
        }
        if (await DialogHelper.ShowAskAsync("Удаление ссылки", $"Вы уверены, что хотите удалить ссылку на '{package.Title}'?", "Удалить", "Отмена"))
        {
            SettingsViewModel.Current.GZDoomInstalls.Remove(package);
        }
    }

    [RelayCommand]
    private static void ToggleDefaultIWad(string? iWadFile)
    {
        if (string.IsNullOrEmpty(iWadFile))
        {
            return;
        }
        SettingsViewModel.Current.DefaultIWadFile = SettingsViewModel.Current.DefaultIWadFile == iWadFile ? "" : iWadFile;
    }

    [RelayCommand]
    private static void OpenFolderIWad(string? iWadFile)
    {
        if (string.IsNullOrEmpty(iWadFile))
        {
            return;
        }
        Process.Start("explorer.exe", "/select," + Path.GetFullPath(iWadFile, FileHelper.IWadFolderPath));
    }

    [RelayCommand]
    private static async Task RemoveIWad(string? iWadFile)
    {
        if (string.IsNullOrEmpty(iWadFile))
        {
            return;
        }
        var title = FileHelper.IWadFileToTitle(iWadFile);
        if (await DialogHelper.ShowAskAsync("Удаление ссылки", $"Вы уверены, что хотите удалить ссылку на '{title}'?", "Удалить", "Отмена"))
        {
            SettingsViewModel.Current.IWadFiles.Remove(iWadFile);
        }
    }
}
