﻿using DoomLauncher.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Storage;

namespace DoomLauncher;

public readonly struct TitleAppId(string title, int appId)
{
    public readonly string title = title;
    public readonly int appId = appId;
}

internal static partial class FileHelper
{
    public static string ConfigFilePath = "";
    public static string ModsFolderPath = "";
    public static string ImagesFolderPath = "";
    public static string PackagesFolderPath = "";
    public static string IWadFolderPath = "";
    public static string EntriesFolderPath = "";


    public static readonly string[] SupportedModExtensions = [".pk3", ".wad", ".zip"];
    public static readonly string[] SupportedImageExtensions = [".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif", ".svg"];

    public static bool ValidateGZDoomPath(string path)
    {
        return !string.IsNullOrEmpty(path) && Path.GetExtension(path) == ".exe" && File.Exists(path);
    }

    public static async Task CopyFileWithConfirmation(StorageFile file, string targetFolder)
    {
        var targetPath = Path.Combine(targetFolder, file.Name);
        if (targetPath != file.Path)
        {
            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }
            if (!File.Exists(targetPath) || await DialogHelper.ShowAskAsync(
                Strings.Resources.DialogFileReplaceTitle,
                Strings.Resources.DialogFileReplaceText(file.Name),
                Strings.Resources.DialogFileReplaceOKAction,
                Strings.Resources.DialogFileReplaceCancelAction
            ))
            {
                using var sourceStream = await file.OpenStreamForReadAsync();
                using var destinationStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write);
                await sourceStream.CopyToAsync(destinationStream);
            }
        }
    }

    public static async Task CopyFileWithConfirmation(Stream sourceStream, string fileName, string targetFolder)
    {
        var targetPath = Path.Combine(targetFolder, fileName);
        if (!Directory.Exists(targetFolder))
        {
            Directory.CreateDirectory(targetFolder);
        }
        if (!File.Exists(targetPath) || await DialogHelper.ShowAskAsync(
            Strings.Resources.DialogFileReplaceTitle,
            Strings.Resources.DialogFileReplaceText(fileName),
            Strings.Resources.DialogFileReplaceOKAction,
            Strings.Resources.DialogFileReplaceCancelAction
        ))
        {
            using var destinationStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write);
            await sourceStream.CopyToAsync(destinationStream);
        }
    }

    public static async Task CreateEntryShortcut(DoomEntryViewModel entry, StorageFile file)
    {
        await FileIO.WriteTextAsync(file, $"[InternetShortcut]\nURL=gzdoomlauncher://launch/?id={entry.Id}\n");
    }

    public static TitleAppId ResolveSteamGame(string steamGame, string defaultSteamGame, string iWadFile, string defaultIWadFile)
    {
        var resolvedSteamGame = string.IsNullOrEmpty(steamGame) ? defaultSteamGame : steamGame;
        if (!string.IsNullOrEmpty(resolvedSteamGame))
        {
            if (resolvedSteamGame == "iwad")
            {
                var resolvedIWad = ResolveIWadFile(iWadFile, defaultIWadFile).ToLower();
                if (!string.IsNullOrEmpty(resolvedIWad) && IWads.TryGetValue(resolvedIWad, out TitleAppId value) && value.appId != 0)
                {
                    return value;
                }
            }
            else if (SteamAppIds.TryGetValue(resolvedSteamGame, out TitleAppId value))
            {
                return value;
            }
        }
        return SteamAppIds["off"];
    }

    public static Dictionary<string, TitleAppId> SteamAppIds = new()
    {
        { "off", new(Strings.Resources.SteamAppIdOff, 0) },
        { "iwad", new(Strings.Resources.SteamAppIdAsIWAD, 0) },
        { "doom", new("Ultimate Doom", 2280) },
        { "doom2", new("Doom II", 2300) },
        { "doom64", new("Doom 64", 1148590) },
        { "heretic", new("Heretic", 2390) },
        { "hexen", new("Hexen", 2360) },
        { "strife", new("Strife", 317040) },
    };

    private static readonly Dictionary<string, TitleAppId> IWads = new()
    {
        { "doom1.wad", new("Doom (Shareware)", 2280) },
        { "doom.wad", new("Ultimate Doom", 2280) },
        { "doom2.wad", new("Doom II", 2300) },
        { "doom2f.wad", new("Doom II (French)", 2300) },
        { "doom64.wad", new("Doom 64", 1148590)},
        { "tnt.wad", new("TNT: Evilution", 0) },
        { "plutonia.wad", new("The Plutonia Experiment", 0) },
        { "heretic1.wad", new("Heretic (Shareware)", 2390) },
        { "heretic.wad", new("Heretic", 2390) },
        { "hexen.wad", new("Hexen", 2360) },
        { "strife1.wad", new("Strife", 317040) },
        { "chex.wad", new("Chex Quest", 0) },
        { "freedoom1.wad", new("Freedoom: Phase 1", 0) },
        { "freedoom2.wad", new("Freedoom: Phase 2", 0) },
        { "freedm.wad", new("FreeDM", 0) },
        { "sigil.wad", new TitleAppId("SIGIL", 0) },
    };

    public static string ResolveIWadFile(string iWadFile, string defaultIWadFile)
    {
        if (!string.IsNullOrEmpty(iWadFile))
        {
            if (SettingsViewModel.Current.IWadFiles.Contains(iWadFile))
            {
                return iWadFile;
            }
        }
        if (SettingsViewModel.Current.IWadFiles.Contains(defaultIWadFile))
        {
            return defaultIWadFile;
        }
        return "";
    }

    public static DoomPackageViewModel? ResolveGZDoomPath(string gZDoomPath, string defaultGZDoomPath)
    {
        if (!string.IsNullOrEmpty(gZDoomPath))
        {
            if (SettingsViewModel.Current.GZDoomInstalls.FirstOrDefault(package => package.Path == gZDoomPath) is DoomPackageViewModel package)
            {
                return package;
            }
        }
        return SettingsViewModel.Current.GZDoomInstalls.FirstOrDefault(package => package.Path == defaultGZDoomPath);
    }

    public static string SteamGameTitle(string steamGame, string defaultSteamGame, string iWadFile, string defaultIWadFile) =>
        ResolveSteamGame(steamGame, defaultSteamGame, iWadFile, defaultIWadFile).title;


    public static string GetIWadFileTitle(string iWadFile, string defaultIWadFile)
    {
        var resolvedIWadFile = ResolveIWadFile(iWadFile, defaultIWadFile);
        if (string.IsNullOrEmpty(resolvedIWadFile))
        {
            return Strings.Resources.NotSelected;
        }
        return IWadFileToTitle(resolvedIWadFile);
    }

    public static string GZDoomPathToTitle(string gZDoomPath, string defaultGZDoomPath)
    {
        var package = ResolveGZDoomPath(gZDoomPath, defaultGZDoomPath);
        if (package == null)
        {
            return Strings.Resources.NotSelected;
        }
        return package.Title;
    }

    public static string PackageToFolderName(DoomPackageViewModel package) => (package.Version?.ToString() ?? "unknown") + "-" + ArchToString(package.Arch);

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

    public static string IWadFileToTitle(string iWadFile)
    {
        var key = iWadFile.ToLower();
        if (IWads.TryGetValue(key, out TitleAppId value))
        {
            return value.title;
        }
        return iWadFile;
    }

    public static string? GetFileVersion(string filePath)
    {
        return FileVersionInfo.GetVersionInfo(filePath)?.ProductVersion;
    }

    public static Version? ParseVersion(string version)
    {
        var match = reVersion().Match(version);
        if (match.Success)
        {
            return new(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value));
        }
        return null;
    }

    [GeneratedRegex("(\\d+)[.-](\\d+)[.-](\\d+)")]
    private static partial Regex reVersion();

    public static string GetFileTitle(string filePath)
    {
        return Path.GetFileName(filePath);
    }
}
