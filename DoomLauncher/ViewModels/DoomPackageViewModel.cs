using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Text.Json.Serialization;

namespace DoomLauncher;

public class DoomPackageViewModel : ObservableObject
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

    public string Title
    {
        get
        {
            if (Arch == AssetArch.notSelected)
            {
                return "По умолчанию";
            }
            return (Version?.ToString() ?? "unknown") + Arch switch
            {
                AssetArch.x64 => "",
                AssetArch.x64legacy => " (legacy)",
                AssetArch.x86 => " 32 bit",
                AssetArch.x86legacy => " 32 bit (legacy)",
                AssetArch.arm64 => " arm64",
                AssetArch.manual => " user",
                _ => " unknown",
            };
        }
    }
}
