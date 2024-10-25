using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoomLauncher.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

#if IS_NON_PACKAGED
using System.Reflection;
#else
using Windows.ApplicationModel;
#endif

namespace DoomLauncher.ViewModels;

public partial class SettingsPageViewModel : ObservableObject
{
    public SettingsPageViewModel()
    {
#if IS_NON_PACKAGED
    var appName = Strings.Resources.UnknownApp;
    var appVersion = Strings.Resources.UnknownVersion;
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

    [ObservableProperty]
    private string onlineSource = SettingsViewModel.Current.OnlineSource;

    [RelayCommand]
    private async Task ApplyOnlineSource()
    {
        if (string.IsNullOrEmpty(OnlineSource))
        {
            SettingsViewModel.Current.OnlineSource = "";
        }
        else {
            EventBus.Progress(Strings.Resources.ProgressLoadingOnlineEntries);
            if (await WebAPI.Current.DownloadEntriesFromJson(OnlineSource) != null)
            {
                SettingsViewModel.Current.OnlineSource = OnlineSource;
            }
            else
            {
                await DialogHelper.ShowAskAsync(
                    Strings.Resources.DialogWrongOnlineEntriesUrlTitle,
                    Strings.Resources.DialogWrongOnlineEntriesUrlText,
                    Strings.Resources.DialogOKAction
                );
            }
            EventBus.Progress(null);
        }
    }

    [RelayCommand]
    private void ResetOnlineSource()
    {
        SettingsViewModel.Current.OnlineSource = OnlineSource = SettingsViewModel.DefaultOnlineSource;
    }

    public string AppVersion { get; }

    [RelayCommand]
    private static async Task AddLocalDoomPackage()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();

        // Need to initialize the picker object with the hwnd / IInitializeWithWindow 
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinApi.HWND);

        // Now we can use the picker object as normal
        picker.FileTypeFilter.Add(".exe");
        picker.CommitButtonText = Strings.Resources.ChooseGZDoom;
        var file = await picker.PickSingleFileAsync();
        if (file != null && FileHelper.ValidateGZDoomPath(file.Path))
        {
            if (!SettingsViewModel.Current.GZDoomInstalls.Any(package => package.Path == file.Path))
            {
                var newPackage = new DoomPackageViewModel
                {
                    Path = file.Path,
                    Version = null,
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
        picker.CommitButtonText = Strings.Resources.ChooseIWAD;
        var files = await picker.PickMultipleFilesAsync();

        foreach (var file in files)
        {
            EventBus.Progress(Strings.Resources.ProgressCopy(file.Name));
            await FileHelper.CopyFileWithConfirmation(file, FileHelper.IWadFolderPath);
            if (!SettingsViewModel.Current.IWadFiles.Contains(file.Name))
            {
                SettingsViewModel.Current.IWadFiles.Add(file.Name);
            }
        }
        EventBus.Progress(null);
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
        if (await DialogHelper.ShowAskAsync(Strings.Resources.DialogRemoveLinkTitle, Strings.Resources.DialogRemoveLinkText(package.Title), Strings.Resources.DialogRemoveAction, Strings.Resources.DialogCancelAction))
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
        if (await DialogHelper.ShowAskAsync(Strings.Resources.DialogRemoveLinkTitle, Strings.Resources.DialogRemoveLinkText(title), Strings.Resources.DialogRemoveAction, Strings.Resources.DialogCancelAction))
        {
            SettingsViewModel.Current.IWadFiles.Remove(iWadFile);
        }
    }

    [RelayCommand]
    private async Task AddLocalFavFile()
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

        foreach (var file in files)
        {
            EventBus.Progress(Strings.Resources.ProgressCopy(file.Name));
            await FileHelper.CopyFileWithConfirmation(file, FileHelper.ModsFolderPath);
            if (!SettingsViewModel.Current.FavoriteFiles.Contains(file.Name))
            {
                SettingsViewModel.Current.FavoriteFiles.Add(file.Name);
            }
        }
        EventBus.Progress(null);
    }

    [RelayCommand]
    private static void OpenFolderFavFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }
        Process.Start("explorer.exe", "/select," + Path.GetFullPath(filePath, FileHelper.ModsFolderPath));
    }

    [RelayCommand]
    private static async Task RemoveFavFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }
        var title = FileHelper.GetFileTitle(filePath);
        if (await DialogHelper.ShowAskAsync(Strings.Resources.DialogRemoveLinkTitle, Strings.Resources.DialogRemoveLinkText(title), Strings.Resources.DialogRemoveAction, Strings.Resources.DialogCancelAction))
        {
            SettingsViewModel.Current.FavoriteFiles.Remove(filePath);
        }
    }
}
