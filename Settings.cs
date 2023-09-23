using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DoomLauncher;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Settings))]
internal partial class JsonSettingsContext : JsonSerializerContext
{
}

public class Settings
{
    public static Settings Current { get; set; } = new();
    public ObservableCollection<GZDoomPackage> GZDoomInstalls { get; set; } = new();
    public ObservableCollection<string> IWadFiles { get; set; } = new();
    public ObservableCollection<string> FavoriteFiles { get; set; } = new();
    public bool CloseOnLaunch { get; set; } = false;
    public int SelectedModIndex { get; set; } = 0;
    public ObservableCollection<DoomEntry> Entries { get; set; } = new();

    public int? WindowX { get; set; } = null;
    public int? WindowY { get; set; } = null;
    public int? WindowWidth { get; set; } = null;
    public int? WindowHeight { get; set; } = null;
    public bool WindowMaximized { get; set; } = false;

    public static void Save()
    {
        var text = JsonSerializer.Serialize(Current, JsonSettingsContext.Default.Settings);
        File.WriteAllText(FileHelper.ConfigFilePath, text);
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

    private string gZDoomPath = "";
    public string GZDoomPath
    {
        get => gZDoomPath;
        set => SetProperty(ref gZDoomPath, value);
    } 

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
        return FileHelper.ArchFromString(str);
    }

    public override void Write(Utf8JsonWriter writer, AssetArch value, JsonSerializerOptions options)
    {
        var str = FileHelper.ArchToString(value);
        writer.WriteStringValue(str);
    }
}
