using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
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

public sealed partial class SettingsPage : Page
{
    private readonly string appVersion;

    public SettingsPage()
    {
        InitializeComponent();
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
        this.appVersion = $"{appName} {appVersion}";
#else
        var version = Package.Current.Id.Version;
        appVersion = $"{Package.Current.DisplayName} {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
#endif
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        EventBus.ChangeBackground(this, null, AnimationDirection.None);
        EventBus.ChangeCaption(this, "Настройки");
        base.OnNavigatedTo(e);
    }

    private static Version? ParseVersion(string version)
    {
        var match = reVersion().Match(version);
        if (match.Success)
        {
            return new(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value));
        }
        return null;
    }

    private async Task LoadReleases()
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
                    var version = ParseVersion(asset.Name);
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
            var newAsset = await PackageSelectorDialog.ShowAsync(XamlRoot, onlinePackages.OrderByDescending(package => package.Version).ThenBy(package => package.Arch).ToList());
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

    private async Task AddLocalGZDoom()
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
            var version = GetFileVersion(file.Path) is string s ? ParseVersion(s) : null;
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

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        await AddLocalGZDoom();
    }

    private async void SearchPackages_Click(object sender, RoutedEventArgs e)
    {
        await LoadReleases();
    }

    private async void BrowseIWadButton_Click(object sender, RoutedEventArgs e)
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
            await FileHelper.CopyFileWithConfirmation(XamlRoot, file, FileHelper.IWadFolderPath);
            if (!Settings.Current.IWadFiles.Contains(file.Name))
            {
                Settings.Current.IWadFiles.Insert(0, file.Name);
            }
        }
        EventBus.Progress(this, null);
    }

    private static string? GetFileVersion(string filePath)
    {
        return FileVersionInfo.GetVersionInfo(filePath)?.ProductVersion;
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
                if (await AskDialog.ShowAsync(XamlRoot, "Удаление ссылки", $"Вы уверены, что хотите удалить ссылку на '{title}'?", "Удалить", "Отмена"))
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
                if (await AskDialog.ShowAsync(XamlRoot, "Удаление ссылки", $"Вы уверены, что хотите удалить ссылку на '{title}'?", "Удалить", "Отмена"))
                {
                    Settings.Current.IWadFiles.Remove(iWadFile);
                }
            }
        }
    }

    [GeneratedRegex("(\\d+)[.-](\\d+)[.-](\\d+)")]
    private static partial Regex reVersion();
}
