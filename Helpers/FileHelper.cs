using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace DoomLauncher;

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

    private static readonly Dictionary<string, string> IWads = new()
    {
        { "doom1.wad", "Doom (Shareware)" },
        { "doom.wad", "Ultimate Doom" },
        { "doom2.wad", "Doom II" },
        { "doom2f.wad", "Doom II (French)"},
        { "doom64.wad", "Doom 64"},
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
    };

    public static string GetIWadTitle(string iWadFile)
    {
        if (string.IsNullOrEmpty(iWadFile))
        {
            return "Не выбрано";
        }
        return IWads.GetValueOrDefault(iWadFile.ToLower(), iWadFile);
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

    public static string GZDoomPackageToTitle(GZDoomPackage? package)
    {
        if (package == null || package.Arch == AssetArch.notSelected)
        {
            return "Не выбрано";
        }
        return (package.Version?.ToString() ?? "unknown") + ArchToTitle(package.Arch);
    }
}
