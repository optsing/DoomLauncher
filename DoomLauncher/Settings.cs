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

public partial class Settings: ObservableObject
{
    public static bool IsCustomTitleBar = false;

    public static Settings Current { get; set; } = new();
    public ObservableCollection<DoomPackageViewModel> GZDoomInstalls { get; set; } = [];
    public ObservableCollection<string> IWadFiles { get; set; } = [];

    [ObservableProperty]
    private string defaultGZDoomPath = "";

    [ObservableProperty]
    private string defaultIWadFile = "";

    public ObservableCollection<string> FavoriteFiles { get; set; } = [];
    public bool CloseOnLaunch { get; set; } = false;

    [ObservableProperty]
    private string steamGame = "off";

    public int SelectedModIndex { get; set; } = 0;
    public ObservableCollection<DoomEntry> Entries { get; set; } = [];

    public int? WindowX { get; set; } = null;
    public int? WindowY { get; set; } = null;
    public int? WindowWidth { get; set; } = null;
    public int? WindowHeight { get; set; } = null;
    public bool WindowMaximized { get; set; } = false;

    public void Save()
    {
        var text = JsonSerializer.Serialize(this, JsonSettingsContext.Default.Settings);
        File.WriteAllText(FileHelper.ConfigFilePath, text);
    }
}

public partial class DoomEntry: ObservableObject
{
    [ObservableProperty]
    private string name = "";

    [ObservableProperty]
    private string description = "";

    [ObservableProperty]
    private string longDescription = "";

    [ObservableProperty]
    private string gZDoomPath = "";

    [ObservableProperty]
    private string iWadFile = "";

    public string SteamGame { get; set; } = "";
    public bool UniqueConfig { get; set; } = false;
    public bool UniqueSavesFolder { get; set; } = false;

    public string Id { get; set; } = "";

    public int SelectedImageIndex { get; set; } = 0;

    public bool Slideshow { get; set; } = true;

    public ObservableCollection<string> ImageFiles { get; set; } = [];
    public ObservableCollection<string> ModFiles { get; set; } = [];

    [ObservableProperty]
    private DateTime? created = null;

    [ObservableProperty]
    private DateTime? lastLaunch = null;

    [ObservableProperty]
    private TimeSpan? playTime;
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
