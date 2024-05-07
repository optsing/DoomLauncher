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

public class SettingsViewModel : ObservableObject
{
    public static bool IsCustomTitleBar = false;

    public static SettingsViewModel Current { get; set; } = new();
    public ObservableCollection<DoomPackageViewModel> GZDoomInstalls { get; set; } = [];
    public ObservableCollection<string> IWadFiles { get; set; } = [];

    public string DefaultGZDoomPath { get => defaultGZDoomPath; set => SetProperty(ref defaultGZDoomPath, value); }
    private string defaultGZDoomPath = "";

    public string DefaultIWadFile { get => defaultIWadFile; set => SetProperty(ref defaultIWadFile, value); }
    private string defaultIWadFile = "";

    public ObservableCollection<string> FavoriteFiles { get; set; } = [];
    public bool CloseOnLaunch { get; set; } = false;

    public string SteamGame { get => steamGame; set => SetProperty(ref steamGame, value); }
    private string steamGame = "off";

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
