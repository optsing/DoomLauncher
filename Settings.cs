using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Windows.Storage;

namespace DoomLauncher;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Settings))]
internal partial class JsonSettingsContext : JsonSerializerContext
{
}

public class Settings
{
    public ObservableCollection<GZDoomPackage> GZDoomInstalls { get; set; } = new();
    public ObservableCollection<string> IWadFiles { get; set; } = new();
    public bool CloseOnLaunch { get; set; } = false;
    public int SelectedModIndex { get; set; } = 0;
    public ObservableCollection<DoomEntry> Entries { get; set; } = new();

    public int? WindowX { get; set; } = null;
    public int? WindowY { get; set; } = null;
    public int? WindowWidth { get; set; } = null;
    public int? WindowHeight { get; set; } = null;
    public bool WindowMaximized { get; set; } = false;

    public static bool ValidateGZDoomPath(string path)
    {
        return !string.IsNullOrEmpty(path) && Path.GetExtension(path) == ".exe" && File.Exists(path);
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

    public static readonly string[] SupportedModExtensions = new[] { ".pk3", ".wad", ".zip" };
    public static readonly string[] SupportedImageExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif", ".svg" };

    public static readonly WebAPI WebAPI = new("GZDoom Launcher");

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
}

public class GZDoomPackage: ObservableObject
{
    public string Path { get; set; } = "";

    private Version? version = null;
    public Version? Version
    {
        get => version;
        set => SetProperty(ref version, value);
    }

    [JsonConverter(typeof(AssetArchJsonConverter))]
    public AssetArch Arch { get; set; } = AssetArch.unknown;
}

public partial class DoomEntry: ObservableObject
{
    private string name = "";
    private string description = "";
    private string longDescription = "";

    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    public string Description
    {
        get => description;
        set => SetProperty(ref description, value);
    }

    public string LongDescription
    {
        get => longDescription;
        set => SetProperty(ref longDescription, value);
    }

    public string GZDoomPath { get; set; } = "";

    private string iWadFile = "";
    public string IWadFile
    {
        get => iWadFile;
        set => SetProperty(ref iWadFile, value);
    }

    public bool UniqueConfig { get; set; } = false;
    public bool UniqueSavesFolder { get; set; } = false;

    public string Id { get; set; } = "";

    public int SelectedImageIndex { get; set; } = 0;

    public bool Slideshow { get; set; } = true;

    public ObservableCollection<string> ImageFiles { get; set; } = new();
    public ObservableCollection<string> ModFiles { get; set; } = new();

    private DateTime? lastLaunch = null;

    public DateTime? LastLaunch
    {
        get => lastLaunch;
        set => SetProperty(ref lastLaunch, value);
    }
}

public class AssetArchJsonConverter : JsonConverter<AssetArch>
{
    public override AssetArch Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString();
        return SettingsPage.ArchFromString(str);
    }

    public override void Write(Utf8JsonWriter writer, AssetArch value, JsonSerializerOptions options)
    {
        var str = SettingsPage.ArchToString(value);
        writer.WriteStringValue(str);
    }
}