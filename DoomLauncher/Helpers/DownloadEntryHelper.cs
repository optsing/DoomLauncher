using DoomLauncher.ViewModels;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.Zip;
using System;
using System.Collections.Generic;
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
    RAR
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
    public static async Task<List<DoomEntryViewModel>> InstallEntry(DownloadEntry entry, string version, DownloadEntryType type, bool createEntries, Action<string?> SetProgress)
    {
        List<DoomEntryViewModel> result = [];
        var targetVersion = entry.Versions.GetValueOrDefault(version) ?? throw new Exception("Version not found");
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
            try
            {
                SetProgress(Strings.Resources.ProgressLongDownload);
                using var stream = await WebAPI.Current.DownloadUrl(url);
                var fileName = targetVersion.InstallTypeAsIsFileName;
                await FileHelper.CopyFileWithConfirmation(stream, fileName, targetFolder);
                if (!targetList.Contains(fileName))
                {
                    targetList.Add(fileName);
                }
                if (createEntries)
                {
                    result.Add(new DoomEntryViewModel()
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = Path.GetFileNameWithoutExtension(fileName),
                        Created = DateTime.Now,
                        LongDescription = entry.Description ?? "",
                        IWadFile = type == DownloadEntryType.IWAD ? fileName : "",
                        ModFiles = type == DownloadEntryType.File ? [fileName] : [],
                    });
                }
            }
            finally
            {
                SetProgress(null);
            }
        }
        else if (targetVersion.InstallType == DownloadEntryInstallType.Zip || targetVersion.InstallType == DownloadEntryInstallType.RAR)
        {
            if (targetVersion.InstallTypeArchiveFileNames == null)
            {
                throw new Exception("Archive file names required for InstallType Zip or RAR");
            }
            try
            {
                SetProgress(Strings.Resources.ProgressDownloadAndExtractArchive);
                using var stream = await WebAPI.Current.DownloadUrl(url);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                ms.Seek(0, SeekOrigin.Begin);
                using IArchive archive = targetVersion.InstallType == DownloadEntryInstallType.RAR
                    ? RarArchive.Open(ms)
                    : ZipArchive.Open(ms);
                List<(string fileName, IArchiveEntry entry)> archiveEntries = [];
                foreach (var (fileName, path) in targetVersion.InstallTypeArchiveFileNames)
                {
                    var archiveEntry = archive.Entries.FirstOrDefault(ae => ae.Key == path) ?? throw new Exception($"File '{path}' not found in archive");
                    archiveEntries.Add((fileName, archiveEntry) );
                }
                foreach (var (fileName, archiveEntry) in archiveEntries)
                {
                    SetProgress(Strings.Resources.ProgressExtract(fileName));
                    using var fileStream = archiveEntry.OpenEntryStream();
                    await FileHelper.CopyFileWithConfirmation(fileStream, fileName, targetFolder);
                    if (!targetList.Contains(fileName))
                    {
                        targetList.Add(fileName);
                    }
                    if (createEntries)
                    {
                        result.Add(new DoomEntryViewModel()
                        {
                            Id = Guid.NewGuid().ToString(),
                            Name = Path.GetFileNameWithoutExtension(fileName),
                            Created = DateTime.Now,
                            LongDescription = entry.Description ?? "",
                            IWadFile = type == DownloadEntryType.IWAD ? fileName : "",
                            ModFiles = type == DownloadEntryType.File ? [fileName] : [],
                        });
                    }
                }             
            }
            finally
            {
                SetProgress(null);
            }
        }
        return result;
    }

    public static async Task InstallPort(DownloadPort port, string version, Action<string?> SetProgress)
    {
        if (!Version.TryParse(version, out var parsedVersion))
        {
            throw new Exception("Version can't be parsed");
        }
        var targetUrl = port.Versions.GetValueOrDefault(version) ?? throw new Exception("Version not found");
        try
        {
            SetProgress(Strings.Resources.ProgressDownload);
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
        }
        finally
        {
            SetProgress(null);
        }
    }
}
