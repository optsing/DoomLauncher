using DoomLauncher.Helpers;
using DoomLauncher.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            Directory.CreateDirectory(targetFolder);
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
        Directory.CreateDirectory(targetFolder);
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

    public static TitleAppId ResolveSteamGame(string steamGame, string defaultSteamGame)
    {
        var resolvedSteamGame = string.IsNullOrEmpty(steamGame) ? defaultSteamGame : steamGame;
        if (SteamAppIds.TryGetValue(resolvedSteamGame, out TitleAppId value))
        {
            return value;
        }
        return SteamAppIds["off"];
    }

    public static Dictionary<string, TitleAppId> SteamAppIds = new()
    {
        { "off", new(Strings.Resources.SteamAppIdOff, 0) },
        { "doom", new("DOOM + DOOM II", 2280) },
        { "doom2", new("Doom II", 2300) },
        { "doom64", new("Doom 64", 1148590) },
        { "heretic", new("Heretic", 2390) },
        { "hexen", new("Hexen", 2360) },
        { "strife", new("Strife", 317040) },
    };

    private static readonly Dictionary<string, string> IWads = new()
    {
        { "doom1.wad", "Doom (Shareware)" },
        { "doom.wad", "Ultimate Doom" },
        { "doom2.wad", "Doom II" },
        { "doom2f.wad", "Doom II (French)" },
        { "doom64.wad", "Doom 64" },
        { "tnt.wad", "TNT: Evilution" },
        { "plutonia.wad", "The Plutonia Experiment" },
        { "heretic1.wad", "Heretic (Shareware)" },
        { "heretic.wad", "Heretic" },
        { "hexen.wad", "Hexen" },
        { "strife1.wad", "Strife" },
        { "chex.wad", "Chex Quest" },
        { "freedoom1.wad", "Freedoom: Phase 1" },
        { "freedoom2.wad", "Freedoom: Phase 2" },
        { "freedm.wad", "FreeDM" },
        { "sigil.wad", "SIGIL" },
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

    public static string SteamGameTitle(string steamGame, string defaultSteamGame) =>
        ResolveSteamGame(steamGame, defaultSteamGame).title;


    public static string GetIWadFileTitle(string iWadFile, string defaultIWadFile)
    {
        var resolvedIWadFile = ResolveIWadFile(iWadFile, defaultIWadFile);
        if (string.IsNullOrEmpty(resolvedIWadFile))
        {
            return Strings.Resources.NotSelected;
        }
        return IWadFileToTitle(resolvedIWadFile);
    }

    public static string GZDoomPathToShortTitle(string gZDoomPath, string defaultGZDoomPath)
    {
        var package = ResolveGZDoomPath(gZDoomPath, defaultGZDoomPath);
        if (package == null)
        {
            return Strings.Resources.NotSelected;
        }
        return package.Arch == AssetArch.manual ? Strings.Resources.DoomPageGZDoomCustom : package.Title;
    }

    public static string VersionAndArchToFolderName(Version? version, AssetArch arch) => (version?.ToString() ?? "unknown") + "-" + ArchToString(arch);

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
        if (IWads.TryGetValue(iWadFile.ToLower(), out var title))
        {
            return title;
        }
        return iWadFile;
    }

    public static string GetFileTitle(string filePath)
    {
        return Path.GetFileName(filePath);
    }
}
