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
/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>


public class GZDoomFileAsset
{
    public string Name { get; set; } = "";
    public Version? Version { get; set; } = null;
    public AssetArch Arch { get; set; } = AssetArch.unknown;
    public string DownloadUrl { get; set; } = "";
}

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
    private readonly Settings settings;

    public SettingsPage(IntPtr hWnd, Settings settings, string dataFolderPath)
    {
        this.InitializeComponent();
        this.hWnd = hWnd;
        this.settings = settings;
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

    private static string ArchToTitle(AssetArch arch) => arch switch
    {
        AssetArch.x64 => "",
        AssetArch.x64legacy => " (legacy)",
        AssetArch.x86 => " 32 bit",
        AssetArch.x86legacy => " 32 bit (legacy)",
        AssetArch.arm64 => " arm64",
        AssetArch.manual => " user",
        _ => " unknown",
    };

    private static string AssetToFolderName(GZDoomFileAsset asset) => (asset.Version?.ToString() ?? "unknown") + "-" + ArchToString(asset.Arch);

    public static string ArchToString(AssetArch arch) => arch switch
    {
        AssetArch.x64 => "x64",
        AssetArch.x64legacy => "x64-legacy",
        AssetArch.x86 => "x86",
        AssetArch.x86legacy => "x86-legacy",
        AssetArch.arm64 => "arm64",
        AssetArch.manual => "manual",
        _ => "unknown",
    };

    public static AssetArch ArchFromString(string? arch) => arch switch
    {
        "x64" => AssetArch.x64,
        "x64-legacy" => AssetArch.x64legacy,
        "x86" => AssetArch.x86,
        "x86-legacy" => AssetArch.x86legacy,
        "arm64" => AssetArch.arm64,
        "manual" => AssetArch.manual,
        _ => AssetArch.unknown,
    };

    private async Task LoadReleases()
    {
        OnProgress?.Invoke(this, "Получение списка версий");
        var entries = await Settings.WebAPI.GetGZDoomGitHubReleases();
        var assets = new List<GZDoomFileAsset>();
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
                    if (!settings.GZDoomInstalls.Any(package => package.Version == version && package.Arch == arch))
                    {
                        var newAsset = new GZDoomFileAsset()
                        {
                            Name = $"GZDoom {version?.ToString() ?? "unknown"}{ArchToTitle(arch)}",
                            Arch = arch,
                            Version = version,
                            DownloadUrl = asset.DownloadUrl,
                        };
                        assets.Add(newAsset);
                    }
                }
            }
        }
        OnProgress?.Invoke(this, null);
        {
            var newAsset = await PackageSelectorDialog.ShowAsync(XamlRoot, assets.OrderByDescending(asset => asset.Version).ThenBy(asset => asset.Arch).ToList());
            if (newAsset != null)
            {
                await DownloadPackage(newAsset);
            }
        }
    }

    private async Task DownloadPackage(GZDoomFileAsset asset)
    {
        if (!string.IsNullOrEmpty(asset.DownloadUrl))
        {
            OnProgress?.Invoke(this, "Загрузка и извлечение архива");
            try
            {
                using var stream = await Settings.WebAPI.DownloadUrl(asset.DownloadUrl);
                using var zipArchive = new ZipArchive(stream, ZipArchiveMode.Read);
                var targetPath = Path.Combine(packagesFolderPath, AssetToFolderName(asset));
                await Task.Run(
                    () => zipArchive.ExtractToDirectory(targetPath, overwriteFiles: true)
                );
                var newPackage = new GZDoomPackage()
                {
                    Path = Path.Combine(targetPath, "gzdoom.exe"),
                    Version = asset.Version,
                    Arch = asset.Arch,
                };
                settings.GZDoomInstalls.Insert(0, newPackage);
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
        if (file != null && Settings.ValidateGZDoomPath(file.Path))
        {
            var version = GetFileVersion(file.Path) is string s ? ParseVersion(s) : null;
            if (settings.GZDoomInstalls.FirstOrDefault(package => package.Path == file.Path) is GZDoomPackage package)
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
                settings.GZDoomInstalls.Insert(0, newPackage);
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
            await Settings.CopyFileWithConfirmation(XamlRoot, file, iWadFolderPath);
            if (!settings.IWadFiles.Contains(file.Name))
            {
                settings.IWadFiles.Add(file.Name);
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
                Process.Start("explorer.exe", "/select," + package.Path);
            }
        }
    }

    private async void RemovePackage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el)
        {
            if (el.DataContext is GZDoomPackage package)
            {
                var title = GZDoomPackageToTitle(package.Version, package.Arch);
                if (await AskDialog.ShowAsync(XamlRoot, "Удаление ссылки", $"Вы уверены, что хотите удалить ссылку на '{title}'?", "Удалить", "Отмена"))
                {
                    settings.GZDoomInstalls.Remove(package);
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
                Process.Start("explorer.exe", "/select," + Path.Combine(iWadFolderPath, iWadFile));
            }
        }
    }

    private async void RemoveIWad_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el)
        {
            if (el.DataContext is string iWadFile)
            {
                var title = IWadFileToTitle(iWadFile);
                if (await AskDialog.ShowAsync(XamlRoot, "Удаление ссылки", $"Вы уверены, что хотите удалить ссылку на '{title}'?", "Удалить", "Отмена"))
                {
                    settings.IWadFiles.Remove(iWadFile);
                }
            }
        }
    }

    public static string GZDoomPackageToTitle(Version? version, AssetArch arch)
    {
        if (arch == AssetArch.notSelected)
        {
            return "Не выбрано";
        }
        return $"GZDoom {version?.ToString() ?? "unknown"}{ArchToTitle(arch)}";
    }

    public static string IWadFileToTitle(string iWadFileName)
    {
        return Settings.IWads.GetValueOrDefault(iWadFileName.ToLower(), iWadFileName);
    }

    [GeneratedRegex("(\\d+)[.-](\\d+)[.-](\\d+)")]
    private static partial Regex reVersion();
}
