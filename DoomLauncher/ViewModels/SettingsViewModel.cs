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
    public static bool IsCustomTitleBar = false;

    public static SettingsViewModel Current { get; set; } = new();
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
    public ObservableCollection<DoomEntryViewModel> Entries { get; set; } = [];

    public int? WindowX { get; set; } = null;
    public int? WindowY { get; set; } = null;
    public int? WindowWidth { get; set; } = null;
    public int? WindowHeight { get; set; } = null;
    public bool WindowMaximized { get; set; } = false;

    public void Save()
    {
        var text = JsonSerializer.Serialize(this, JsonSettingsContext.Default.SettingsViewModel);
        File.WriteAllText(FileHelper.ConfigFilePath, text);
    }
}
