using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace DoomLauncher;

public readonly struct TitleAppId
{
    public readonly string title;
    public readonly int appId;

    public TitleAppId(string title, int appId)
    {
        this.title = title;
        this.appId = appId;
    }
}

internal static class FileHelper
{
    public static string ConfigFilePath = "";
    public static string ModsFolderPath = "";
    public static string ImagesFolderPath = "";
    public static string PackagesFolderPath = "";
    public static string IWadFolderPath = "";
    public static string EntriesFolderPath = "";


    public static readonly string[] SupportedModExtensions = new[] { ".pk3", ".wad", ".zip" };
    public static readonly string[] SupportedImageExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif", ".svg" };

    public static bool ValidateGZDoomPath(string path)
    {
        return !string.IsNullOrEmpty(path) && Path.GetExtension(path) == ".exe" && File.Exists(path);
    }

    public static async Task CopyFileWithConfirmation(XamlRoot xamlRoot, StorageFile file, string targetFolder)
    {
        var targetPath = Path.Combine(targetFolder, file.Name);
        if (targetPath != file.Path)
        {
            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }
            if (!File.Exists(targetPath) || await AskDialog.ShowAsync(
                xamlRoot,
                "Добавление с заменой",
                $"Файл '{file.Name}' уже существует в папке лаунчера.\nЗаменить?",
                "Заменить",
                "Не заменять"
            ))
            {
                using var sourceStream = await file.OpenStreamForReadAsync();
                using var destinationStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write);
                await sourceStream.CopyToAsync(destinationStream);
            }
        }
    }

    public static async Task CopyFileWithConfirmation(XamlRoot xamlRoot, Stream sourceStream, string fileName, string targetFolder)
    {
        var targetPath = Path.Combine(targetFolder, fileName);
        if (!Directory.Exists(targetFolder))
        {
            Directory.CreateDirectory(targetFolder);
        }
        if (!File.Exists(targetPath) || await AskDialog.ShowAsync(
            xamlRoot,
            "Добавление с заменой",
            $"Файл '{fileName}' уже существует в папке лаунчера.\nЗаменить?",
            "Заменить",
            "Не заменять"
        ))
        {
            using var destinationStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write);
            await sourceStream.CopyToAsync(destinationStream);
        }
    }

    public static async Task CreateEntryShortcut(DoomEntry entry, StorageFile file)
    {
        await FileIO.WriteTextAsync(file, $"[InternetShortcut]\nURL=gzdoomlauncher://launch/?id={entry.Id}\n");
    }

    public static int GetSteamAppIdForEntry (DoomEntry entry)
    {
        var steamGame = string.IsNullOrEmpty(entry.SteamGame) ? Settings.Current.SteamGame : entry.SteamGame;
        if (!string.IsNullOrEmpty(steamGame) && steamGame !=  "off")
        {
            if (steamGame == "iwad")
            {
                var resolvedIWad = ResolveIWadFile(entry.IWadFile, Settings.Current.DefaultIWadFile).ToLower();
                if (!string.IsNullOrEmpty(resolvedIWad) && IWads.ContainsKey(resolvedIWad))
                {
                    return IWads[resolvedIWad].appId;
                }
            }
            else if (SteamAppIds.ContainsKey(steamGame))
            {
                return SteamAppIds[steamGame].appId;
            }
        }
        return 0;
    }

    public static Dictionary<string, TitleAppId> SteamAppIds = new()
    {
        { "off", new("Отключено", 0) },
        { "iwad", new("Согласно iWad", -1) },
        { "doom", new("Ultimate Doom", 2280) },
        { "doom2", new("Doom 2", 2300) },
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
    };

    public static string ResolveIWadFile(string iWadFile, string defaultIWadFile)
    {
        if (!string.IsNullOrEmpty(iWadFile))
        {
            if (Settings.Current.IWadFiles.Contains(iWadFile))
            {
                return iWadFile;
            }
        }
        if (Settings.Current.IWadFiles.Contains(defaultIWadFile))
        {
            return defaultIWadFile;
        }
        return "";
    }

    public static GZDoomPackage? ResolveGZDoomPath(string gZDoomPath, string defaultGZDoomPath)
    {
        if (!string.IsNullOrEmpty(gZDoomPath))
        {
            if (Settings.Current.GZDoomInstalls.FirstOrDefault(package => package.Path == gZDoomPath) is GZDoomPackage package)
            {
                return package;
            }
        }
        return Settings.Current.GZDoomInstalls.FirstOrDefault(package => package.Path == defaultGZDoomPath);
    }

    public static string GetIWadFileTitle(string iWadFile, string defaultIWadFile) {
        var resolvedIWadFile = ResolveIWadFile(iWadFile, defaultIWadFile);
        if (string.IsNullOrEmpty(resolvedIWadFile))
        {
            return "Не выбрано";
        }
        return IWadFileToTitle(resolvedIWadFile);
    }

    public static string GZDoomPathToTitle(string gZDoomPath, string defaultGZDoomPath)
    {
        var package = ResolveGZDoomPath(gZDoomPath, defaultGZDoomPath);
        if (package == null)
        {
            return "Не выбрано";
        }
        return GZDoomPackageToTitle(package);
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

    public static string PackageToFolderName(GZDoomPackage package) => (package.Version?.ToString() ?? "unknown") + "-" + ArchToString(package.Arch);

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
        if (IWads.ContainsKey(key))
        {
            return IWads[key].title;
        }
        return iWadFile;
    }

    public static string GZDoomPackageToTitle(GZDoomPackage? package)
    {
        if (package == null || package.Arch == AssetArch.notSelected)
        {
            return "По умолчанию";
        }
        return (package.Version?.ToString() ?? "unknown") + ArchToTitle(package.Arch);
    }
}
