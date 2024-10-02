using DoomLauncher.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DoomLauncher.Helpers;


public enum DownloadEntryType
{
    File,
    IWAD,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DownloadEntryInstallType
{
    AsIs,
    Zip,
    User
}

[JsonSerializable(typeof(DownloadEntryList))]
internal partial class JsonDownloadEntryContext : JsonSerializerContext { }

public class DownloadEntryList
{
    public required List<DownloadPort> Ports { get; init; }
    public required List<DownloadEntry> IWADs { get; init; }
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
    public required DownloadEntryInstallType InstallType { get; init; }
    public string? InstallTypeAsIsFileName { get; init; }
    public Dictionary<string, string>? InstallTypeZipFileNames { get; init; }
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
        if (targetVersion.InstallType == DownloadEntryInstallType.AsIs)
        {
            if (targetVersion.InstallTypeAsIsFileName == null)
            {
                throw new Exception("File name required for InstallType == AsIs");
            }
            SetProgress(Strings.Resources.ProgressLongDownload);
            try
            {
                using var stream = await WebAPI.Current.DownloadUrl(targetVersion.Url);
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
            SetProgress(null);
        }
        else if (targetVersion.InstallType == DownloadEntryInstallType.Zip)
        {
            if (targetVersion.InstallTypeZipFileNames == null)
            {
                throw new Exception("Extract file names required for InstallType == Zip");
            }
            SetProgress(Strings.Resources.ProgressDownloadAndExtractArchive);
            try
            {
                using var stream = await WebAPI.Current.DownloadUrl(targetVersion.Url);
                using var zipArchive = new ZipArchive(stream, ZipArchiveMode.Read);
                List<(string name, ZipArchiveEntry entry)> zipEntries = [];
                foreach (var (name, path) in targetVersion.InstallTypeZipFileNames)
                {
                    var zipEntry = zipArchive.GetEntry(path) ?? throw new Exception($"File '{path}' not found in archive");
                    zipEntries.Add((name, zipEntry));
                }
                foreach (var (name, zipEntry) in zipEntries)
                {
                    SetProgress(Strings.Resources.ProgressExtract(zipEntry.Name));
                    using var fileStream = zipEntry.Open();
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
            SetProgress(null);
        }
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
        SetProgress(Strings.Resources.ProgressDownloadAndExtractArchive);
        try
        {
            using var stream = await WebAPI.Current.DownloadUrl(targetUrl);
            using var zipArchive = new ZipArchive(stream, ZipArchiveMode.Read);
            var folderName = FileHelper.VersionAndArchToFolderName(parsedVersion, port.Arch);
            var targetPath = Path.Combine(FileHelper.PackagesFolderPath, folderName);
            await Task.Run(
                () => zipArchive.ExtractToDirectory(targetPath, overwriteFiles: true)
            );
            var newPackage = new DoomPackageViewModel()
            {
                Path = Path.Combine(folderName, "gzdoom.exe"),
                Version = parsedVersion,
                Arch = port.Arch,
            };
            SettingsViewModel.Current.GZDoomInstalls.Add(newPackage);
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
