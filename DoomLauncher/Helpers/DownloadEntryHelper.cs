using DoomLauncher.ViewModels;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.Zip;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DoomLauncher.Helpers;


public enum DownloadEntryType
{
    File,
    IWAD,
}

[JsonConverter(typeof(JsonStringEnumConverter<DownloadEntryInstallType>))]
public enum DownloadEntryInstallType
{
    AsIs,
    Zip,
    RAR,
    User
}

[JsonConverter(typeof(JsonStringEnumConverter<DownloadEntryUrlType>))]
public enum DownloadEntryUrlType
{
    Direct,
    ModDB
}

[JsonSerializable(typeof(DownloadEntryList))]
internal partial class JsonDownloadEntryContext : JsonSerializerContext { }

public class DownloadEntryList
{
    public required List<DownloadPort> Ports { get; init; }
    public required List<DownloadEntry> IWAD { get; init; }
    public required List<DownloadEntry> Files { get; init; }
}

public class DownloadEntry
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? Homepage { get; init; }
    public required Dictionary<string, DownloadEntryVersion> Versions { get; init; }
}

public class DownloadEntryVersion
{
    public required string Url { get; init; }
    public DownloadEntryUrlType UrlType { get; set; } = DownloadEntryUrlType.Direct;
    public required DownloadEntryInstallType InstallType { get; init; }
    public string? InstallTypeAsIsFileName { get; init; }
    public Dictionary<string, string>? InstallTypeArchiveFileNames { get; init; }
}

public class DownloadPort
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? Homepage { get; init; }
    public AssetArch Arch { get; init; }
    public required Dictionary<string, string> Versions { get; init; }
}

public static class DownloadEntryHelper
{
    public static async Task<bool> InstallEntry(DownloadEntry entry, string version, DownloadEntryType type, Action<string?> SetProgress)
    {
        var success = false;
        var targetVersion = entry.Versions.GetValueOrDefault(version) ?? throw new Exception("Version not found");
        if (targetVersion.InstallType == DownloadEntryInstallType.User)
        {
            Process.Start(new ProcessStartInfo() { FileName = targetVersion.Url, UseShellExecute = true });
            return true;
        }
        var targetFolder = type switch
        {
            DownloadEntryType.File => FileHelper.ModsFolderPath,
            DownloadEntryType.IWAD => FileHelper.IWadFolderPath,
            _ => throw new NotSupportedException(),
        };
        var targetList = type switch
        {
            DownloadEntryType.File => SettingsViewModel.Current.FavoriteFiles,
            DownloadEntryType.IWAD => SettingsViewModel.Current.IWadFiles,
            _ => throw new NotSupportedException(),
        };
        var url = targetVersion.UrlType switch
        {
            DownloadEntryUrlType.Direct => targetVersion.Url,
            DownloadEntryUrlType.ModDB => await WebAPI.Current.GetDirectUrlFromModDB(targetVersion.Url),
            _ => throw new NotSupportedException(),
        };
        if (targetVersion.InstallType == DownloadEntryInstallType.AsIs)
        {
            if (targetVersion.InstallTypeAsIsFileName == null)
            {
                throw new Exception("File name required for InstallType == AsIs");
            }
            SetProgress(Strings.Resources.ProgressLongDownload);
            try
            {
                using var stream = await WebAPI.Current.DownloadUrl(url);
                await FileHelper.CopyFileWithConfirmation(stream, targetVersion.InstallTypeAsIsFileName, targetFolder);
                if (!targetList.Contains(targetVersion.InstallTypeAsIsFileName))
                {
                    targetList.Add(targetVersion.InstallTypeAsIsFileName);
                }
                success = true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
        }
        else if (targetVersion.InstallType == DownloadEntryInstallType.Zip || targetVersion.InstallType == DownloadEntryInstallType.RAR)
        {
            if (targetVersion.InstallTypeArchiveFileNames == null)
            {
                throw new Exception("Archive file names required for InstallType Zip or RAR");
            }
            SetProgress(Strings.Resources.ProgressDownloadAndExtractArchive);
            try
            {
                using var stream = await WebAPI.Current.DownloadUrl(url);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                ms.Seek(0, SeekOrigin.Begin);
                using IArchive archive = targetVersion.InstallType == DownloadEntryInstallType.RAR
                    ? RarArchive.Open(ms)
                    : ZipArchive.Open(ms);
                List<(string name, IArchiveEntry entry)> archiveEntries = [];
                foreach (var (name, path) in targetVersion.InstallTypeArchiveFileNames)
                {
                    var archiveEntry = archive.Entries.FirstOrDefault(ae => ae.Key == path) ?? throw new Exception($"File '{path}' not found in archive");
                    archiveEntries.Add((name, archiveEntry) );
                }
                foreach (var (name, archiveEntry) in archiveEntries)
                {
                    SetProgress(Strings.Resources.ProgressExtract(archiveEntry.Key!));
                    using var fileStream = archiveEntry.OpenEntryStream();
                    await FileHelper.CopyFileWithConfirmation(fileStream, name, targetFolder);
                    if (!targetList.Contains(name))
                    {
                        targetList.Add(name);
                    }
                }
                success = true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
        }
        SetProgress(null);
        return success;
    }

    public static async Task<bool> InstallPort(DownloadPort port, string version, Action<string?> SetProgress)
    {
        var success = false;
        if (!Version.TryParse(version, out var parsedVersion))
        {
            throw new Exception("Version can't be parsed");
        }
        var targetUrl = port.Versions.GetValueOrDefault(version) ?? throw new Exception("Version not found");
        SetProgress(Strings.Resources.ProgressDownload);
        try
        {
            using var stream = await WebAPI.Current.DownloadUrl(targetUrl);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            ms.Seek(0, SeekOrigin.Begin);
            using var zipArchive = ZipArchive.Open(ms);
            var folderName = FileHelper.VersionAndArchToFolderName(parsedVersion, port.Arch);
            var targetPath = Path.Combine(FileHelper.PackagesFolderPath, folderName);
            var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            await Task.Run(
                () => {
                    zipArchive.ExtractToDirectory(
                        targetPath, 
                        perc => dispatcherQueue.TryEnqueue(() => SetProgress(Strings.Resources.ProgressExtractPercents((int)Math.Ceiling(perc * 100))))
                    );
                }
            );
            var newPackage = new DoomPackageViewModel()
            {
                Path = Path.Combine(folderName, "gzdoom.exe"),
                Version = parsedVersion,
                Arch = port.Arch,
            };
            if (!SettingsViewModel.Current.GZDoomInstalls.Any(p => p.Path == newPackage.Path))
            {
                SettingsViewModel.Current.GZDoomInstalls.Add(newPackage);
            }
            success = true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }
        SetProgress(null);
        return success;
    }
}
