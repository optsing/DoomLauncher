using DoomLauncher.ViewModels;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.Zip;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
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
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Homepage { get; init; }
    public List<string> Images { get; set; } = [];
    public required Dictionary<string, DownloadEntryVersion> Versions { get; init; }
}

public class DownloadEntryVersion
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public required string Url { get; init; }
    public DownloadEntryUrlType UrlType { get; set; } = DownloadEntryUrlType.Direct;
    public required DownloadEntryInstallType InstallType { get; init; }
    public required List<DownloadEntryVersionEntry> Entries { get; init; }
}

public class DownloadEntryVersionEntry
{
    public string? Name { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public DownloadEntryVersionEntryFile? IWAD { get; init; }
    public List<DownloadEntryVersionEntryFile> Files { get; set; } = [];
    public List<int>? Images { get; init; } = null;
}

public class DownloadEntryVersionEntryFile
{
    public required string Name { get; init; }
    public required string Path { get; init; }
}

public class DownloadPort
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? Homepage { get; init; }
    public List<string> Images { get; set; } = [];
    public AssetArch Arch { get; init; }
    public required Dictionary<string, string> Versions { get; init; }
}

public static class DownloadEntryHelper
{
    private static string TargetFolderFromType(DownloadEntryType type) => type switch
    {
        DownloadEntryType.File => FileHelper.ModsFolderPath,
        DownloadEntryType.IWAD => FileHelper.IWadFolderPath,
        _ => throw new NotSupportedException(),
    };

    private static ObservableCollection<string> TargetListFromType(DownloadEntryType type) => type switch
    {
        DownloadEntryType.File => SettingsViewModel.Current.FavoriteFiles,
        DownloadEntryType.IWAD => SettingsViewModel.Current.IWadFiles,
        _ => throw new NotSupportedException(),
    };

    public static async Task<List<DoomEntryViewModel>> InstallEntry(DownloadEntry entry, string version, bool createEntries, Action<string?> SetProgress)
    {
        List<DoomEntryViewModel> result = [];
        var targetVersion = entry.Versions.GetValueOrDefault(version) ?? throw new Exception("Version not found");
        var url = targetVersion.UrlType switch
        {
            DownloadEntryUrlType.Direct => targetVersion.Url,
            DownloadEntryUrlType.ModDB => await WebAPI.Current.GetDirectUrlFromModDB(targetVersion.Url),
            _ => throw new NotSupportedException(),
        };
        Dictionary<string, string> requiredImages = [];
        List<(DownloadEntryVersionEntryFile file, DownloadEntryType type)> requiredFiles = [];
        foreach (var subEntry in targetVersion.Entries)
        {
            if (subEntry.IWAD != null)
            {
                var file = subEntry.IWAD;
                requiredFiles.Add((file, DownloadEntryType.IWAD));
            }
            foreach (var file in subEntry.Files)
            {
                requiredFiles.Add((file, DownloadEntryType.File));
            }
            if (createEntries)
            {
                List<string> images = [];
                if (requiredImages.Count < entry.Images.Count)
                {
                    if (subEntry.Images == null)
                    {
                        foreach (var imageUrl in entry.Images)
                        {
                            if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
                            {
                                var imageName = Path.GetFileName(uri.LocalPath);
                                requiredImages.Add(imageUrl, imageName);
                                images.Add(imageName);
                            }
                        }
                    }
                    else
                    {
                        foreach (var imageInd in subEntry.Images)
                        {
                            if (imageInd >= 0 && imageInd < entry.Images.Count)
                            {
                                var imageUrl = entry.Images[imageInd];
                                if (!requiredImages.ContainsKey(imageUrl) && Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
                                {
                                    var imageName = Path.GetFileName(uri.LocalPath);
                                    requiredImages.Add(imageUrl, imageName);
                                    images.Add(imageName);
                                }
                            }
                        }
                    }
                }
                result.Add(new DoomEntryViewModel()
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = subEntry.Name ?? entry.Name,
                    Created = DateTime.Now,
                    Description = subEntry.Title ?? targetVersion.Title ?? entry.Title ?? "",
                    LongDescription = subEntry.Description ?? targetVersion.Description ?? entry.Description ?? "",
                    IWadFile = subEntry.IWAD?.Name ?? "",
                    ModFiles = subEntry.Files.Select(file => file.Name).ToList(),
                    ImageFiles = images,
                });
            }
        }
        try
        {
            if (targetVersion.InstallType == DownloadEntryInstallType.AsIs)
            {
                SetProgress(Strings.Resources.ProgressLongDownload);
                using var stream = await WebAPI.Current.DownloadUrl(url);
                foreach (var (file, type) in requiredFiles)
                {
                    SetProgress(Strings.Resources.ProgressCopy(file.Name));
                    await FileHelper.CopyFileWithConfirmation(stream, file.Name, TargetFolderFromType(type));
                    var targetList = TargetListFromType(type);
                    if (!targetList.Contains(file.Name))
                    {
                        targetList.Add(file.Name);
                    }
                }
            }
            else if (targetVersion.InstallType == DownloadEntryInstallType.Zip || targetVersion.InstallType == DownloadEntryInstallType.RAR)
            {
                SetProgress(Strings.Resources.ProgressDownloadAndExtractArchive);
                using var stream = await WebAPI.Current.DownloadUrl(url);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                ms.Seek(0, SeekOrigin.Begin);
                using IArchive archive = targetVersion.InstallType == DownloadEntryInstallType.RAR
                    ? RarArchive.Open(ms)
                    : ZipArchive.Open(ms);
                List<(DownloadEntryVersionEntryFile file, DownloadEntryType type, IArchiveEntry entry)> archiveEntries = [];
                foreach (var (file, type) in requiredFiles)
                {
                    var archiveEntry = archive.Entries.FirstOrDefault(ae => ae.Key == file.Path) ?? throw new Exception($"File '{file.Path}' not found in archive");
                    archiveEntries.Add((file, type, archiveEntry));
                }
                foreach (var (file, type, archiveEntry) in archiveEntries)
                {
                    SetProgress(Strings.Resources.ProgressExtract(file.Name));
                    using var fileStream = archiveEntry.OpenEntryStream();
                    await FileHelper.CopyFileWithConfirmation(fileStream, file.Name, TargetFolderFromType(type));
                    var targetList = TargetListFromType(type);
                    if (!targetList.Contains(file.Name))
                    {
                        targetList.Add(file.Name);
                    }
                }
            }
            foreach (var (imageUrl, imageName) in requiredImages)
            {
                using var imageStream = await WebAPI.Current.DownloadUrl(imageUrl);
                await FileHelper.CopyFileWithConfirmation(imageStream, imageName, FileHelper.ImagesFolderPath);
            }
        }
        finally
        {
            SetProgress(null);
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
                () =>
                {
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
