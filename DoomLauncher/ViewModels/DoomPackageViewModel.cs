using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Text.Json.Serialization;

namespace DoomLauncher.ViewModels;


public partial class DoomPackageViewModel : ObservableObject
{
    public string Path { get; set; } = "";

    public Version? Version { get => version; set => SetProperty(ref version, value); }
    private Version? version = null;

    public AssetArch Arch { get; set; } = AssetArch.unknown;

    [JsonIgnore]
    public string Title
    {
        get
        {
            if (Arch == AssetArch.notSelected)
            {
                return Strings.Resources.DefaultValue;
            }
            if (Arch == AssetArch.manual && System.IO.Path.GetDirectoryName(Path) is string path)
            {
                return path;
            }
            return "GZDoom " + (Version?.ToString() ?? "unknown") + Arch switch
            {
                AssetArch.x64 => "",
                AssetArch.x86 => " 32 bit",
                AssetArch.arm64 => " arm64",
                AssetArch.manual => " user",
                _ => " unknown",
            };
        }
    }
}
