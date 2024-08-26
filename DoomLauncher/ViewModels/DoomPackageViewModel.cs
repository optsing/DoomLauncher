using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DoomLauncher.ViewModels;

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


public partial class DoomPackageViewModel : ObservableObject
{
    public string Path { get; set; } = "";

    public Version? Version { get => version; set => SetProperty(ref version, value); }
    private Version? version = null;

    [JsonConverter(typeof(AssetArchJsonConverter))]
    public AssetArch Arch { get; set; } = AssetArch.unknown;

    public string Title
    {
        get
        {
            if (Arch == AssetArch.notSelected)
            {
                return Strings.Resources.DefaultValue;
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
