using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DoomLauncher.ViewModels;


[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SettingsViewModel))]
internal partial class JsonSettingsContext : JsonSerializerContext
{
}

public partial class SettingsViewModel : ObservableObject
{
    public const string DefaultOnlineSource = "https://raw.githubusercontent.com/optsing/doom-free-files-source/refs/heads/main/index.json";

    public static bool IsCustomTitleBar = false;

    public static SettingsViewModel Current { get; set; } = new();
    public ObservableCollection<DoomPackageViewModel> GZDoomInstalls { get; set; } = [];
    public ObservableCollection<string> IWadFiles { get; set; } = [];

    [ObservableProperty]
    public partial string DefaultGZDoomPath { get; set; } = "";

    [ObservableProperty]
    public partial string DefaultIWadFile { get; set; } = "";

    public ObservableCollection<string> FavoriteFiles { get; set; } = [];
    public bool CloseOnLaunch { get; set; } = false;

    [ObservableProperty]
    public partial string SteamGame { get; set; } = "off";

    [ObservableProperty]
    public partial string OnlineSource { get; set; } = DefaultOnlineSource;

    public string SortOrder { get; set; } = "";

    public int SelectedModIndex { get; set; } = 0;
    public ObservableCollection<DoomEntryViewModel> Entries { get; set; } = [];

    public int? WindowX { get; set; } = null;
    public int? WindowY { get; set; } = null;
    public int? WindowWidth { get; set; } = null;
    public int? WindowHeight { get; set; } = null;
    public bool WindowMaximized { get; set; } = false;

    public static SettingsViewModel? Load()
    {
        var text = File.ReadAllText(FileHelper.ConfigFilePath);
        return JsonSerializer.Deserialize(text, JsonSettingsContext.Default.SettingsViewModel);
    }

    public void Save()
    {
        var text = JsonSerializer.Serialize(this, JsonSettingsContext.Default.SettingsViewModel);
        File.WriteAllText(FileHelper.ConfigFilePath, text);
    }
}
