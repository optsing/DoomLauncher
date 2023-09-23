using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

public sealed partial class SettingsPage : Page
{
    private readonly IntPtr hWnd;
    private readonly string appVersion;
    private readonly string packagesFolderPath;
    private readonly string iWadFolderPath;

    public SettingsPage(IntPtr hWnd, string dataFolderPath)
    {
        this.InitializeComponent();
        this.hWnd = hWnd;
        packagesFolderPath = Path.Combine(dataFolderPath, "gzdoom");
        iWadFolderPath = Path.Combine(dataFolderPath, "iwads");
        try
        {
            var version = Package.Current.Id.Version;
            appVersion = $"Версия {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
        catch {
            if (Assembly.GetExecutingAssembly()?.GetName()?.Version is Version version)
            {
                appVersion = "Версия " + version.ToString();
            }
            else
            {
                appVersion = "Неизвестная версия";
            }
        }
    }

    public event EventHandler<string?>? OnProgress;

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
        OnProgress?.Invoke(this, "Получение списка версий");
        var entries = await WebAPI.Current.GetGZDoomGitHubReleases();
        var onlinePackages = new List<GZDoomPackage>();
        foreach (var entry in entries)
        {
            foreach (var asset in entry.Assets)
            {
                if (asset.Name.EndsWith(".zip") && !asset.Name.Contains("macOS") && !asset.Name.Contains("ci_deps"))
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
        OnProgress?.Invoke(this, null);
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
            OnProgress?.Invoke(this, "Загрузка и извлечение архива");
            try
            {
                using var stream = await WebAPI.Current.DownloadUrl(package.Path);
                using var zipArchive = new ZipArchive(stream, ZipArchiveMode.Read);
                var folderName = FileHelper.PackageToFolderName(package);
                var targetPath = Path.Combine(packagesFolderPath, folderName);
                await Task.Run(
                    () => zipArchive.ExtractToDirectory(targetPath, overwriteFiles: true)
                );
                var newPackage = new GZDoomPackage()
                {
                    Path = Path.Combine(folderName, "gzdoom.exe"),
                    Version = package.Version,
                    Arch = package.Arch,
                };
                Settings.Current.GZDoomInstalls.Add(newPackage);
            }
            finally
            {
                OnProgress?.Invoke(this, null);
            }
        }
    }

    private async Task AddLocalGZDoom()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();

        // Need to initialize the picker object with the hwnd / IInitializeWithWindow 
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

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
                Settings.Current.GZDoomInstalls.Add(newPackage);
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
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

        // Now we can use the picker object as normal
        picker.FileTypeFilter.Add(".wad");
        picker.CommitButtonText = "Выбрать IWad";
        var files = await picker.PickMultipleFilesAsync();

        foreach (var file in files)
        {
            OnProgress?.Invoke(this, $"Копирование: {file.Name}");
            await FileHelper.CopyFileWithConfirmation(XamlRoot, file, iWadFolderPath);
            if (!Settings.Current.IWadFiles.Contains(file.Name))
            {
                Settings.Current.IWadFiles.Add(file.Name);
            }
        }
        OnProgress?.Invoke(this, null);
    }

    private static string? GetFileVersion(string filePath)
    {
        return FileVersionInfo.GetVersionInfo(filePath)?.ProductVersion;
    }

    private void OpenContainFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el)
        {
            if (el.DataContext is GZDoomPackage package)
            {
                Process.Start("explorer.exe", "/select," + Path.GetFullPath(package.Path, packagesFolderPath));
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

    private void OpenIWadFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el)
        {
            if (el.DataContext is string iWadFile)
            {
                Process.Start("explorer.exe", "/select," + Path.GetFullPath(iWadFile, iWadFolderPath));
            }
        }
    }

    private async void RemoveIWad_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el)
        {
            if (el.DataContext is string iWadFile)
            {
                var title = FileHelper.GetIWadTitle(iWadFile);
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
