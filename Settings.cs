using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DoomLauncher;

public class Settings
{
    public string GZDoomPath { get; set; } = "";
    public bool CloseOnLaunch { get; set; } = true;
    public int SelectedModIndex { get; set; } = 0;
    public ObservableCollection<DoomEntry> Entries { get; set; } = new();

    public static bool ValidateGZDoomPath(string path)
    {
        return !string.IsNullOrEmpty(path) && Path.GetExtension(path) == ".exe" && File.Exists(path);
    }

    public static readonly List<KeyValue> IWads = new()
    {
        new () { Key = "", Value = "Не выбрано" },
        new () { Key = "doom1.wad", Value = "Doom (Shareware)" },
        new () { Key = "doom.wad", Value = "Ultimate Doom" },
        new () { Key = "doom2.wad", Value = "Doom II" },
        new () { Key = "doom2f.wad", Value = "Doom II (French)"},
        new () { Key = "doom64.wad", Value = "Doom 64"},
        new () { Key = "tnt.wad", Value = "TNT: Evilution" },
        new () { Key = "plutonia.wad", Value = "The Plutonia Experiment" },
        new () { Key = "heretic1.wad", Value = "Heretic (Shareware)" },
        new () { Key = "heretic.wad", Value = "Heretic" },
        new () { Key = "hexen.wad", Value = "Hexen" },
        new () { Key = "strife1.wad", Value = "Strife" },
        new () { Key = "chex.wad", Value = "Chex Quest" },
        new () { Key = "freedoom1.wad", Value = "Freedoom: Phase 1" },
        new () { Key = "freedoom2.wad", Value = "Freedoom: Phase 2" },
        new () { Key = "freedm.wad", Value = "FreeDM" },
    };
}


public class DoomEntry : INotifyPropertyChanged
{
    private string name;
    private KeyValue iWadFile;

    public event PropertyChangedEventHandler PropertyChanged;

    public string Id { get; set; }

    public string Name
    {
        get => name;
        set
        {
            if (name != value)
            {
                name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
    }
    public KeyValue IWadFile
    {
        get => iWadFile;
        set {
            if (iWadFile != value)
            {
                iWadFile = value;
                OnPropertyChanged(nameof(IWadFile));
            }
        }
    }

    public ObservableCollection<string> ImageFiles { get; set; } = new();
    public ObservableCollection<NamePath> ModFiles { get; set; } = new();

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}


public class NamePath
{
    public string Name
    {
        get; set;
    }

    public string Path
    {
        get; set;
    }

    public NamePath(string path)
    {
        Name = System.IO.Path.GetFileName(path);
        Path = path;
    }
}

public class NamePathConverter : JsonConverter<NamePath>
{
    public override NamePath Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new(reader.GetString());
    }

    public override void Write(Utf8JsonWriter writer, NamePath value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Path);
    }
}

public class KeyValue
{
    public string Key
    {
        get; set;
    }

    public string Value
    {
        get; set;
    }
}

public class KeyValueConverter : JsonConverter<KeyValue>
{
    public override KeyValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var key = reader.GetString();
        var item = Settings.IWads.Find(item => item.Key == key);
        if (item != null)
        {
            return item;
        }
        return Settings.IWads.First();
    }

    public override void Write(Utf8JsonWriter writer, KeyValue value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Key);
    }
}
