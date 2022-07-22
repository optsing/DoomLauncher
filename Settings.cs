using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DoomLauncher;

public class Settings
{
    public string GZDoomPath { get; set; }

    public int SelectedModIndex
    {
        get; set;
    }

    public ObservableCollection<DoomEntry> Entries { get; set; }

    public static readonly List<KeyValue> IWads = new()
    {
        new () { Key = "", Value = "Не выбрано" },
        new () { Key = "doom.wad", Value = "Doom" },
        new () { Key = "doom2.wad", Value = "Doom II" },
        new () { Key = "tnt.wad", Value = "TNT: Evilution" },
        new () { Key = "plutonia.wad", Value = "The Plutonia Experiment" },
        new () { Key = "heretic.wad", Value = "Heretic" },
        new () { Key = "hexen.wad", Value = "Hexen" },
    };
}

public class DoomEntry : INotifyPropertyChanged
{
    private string name;
    private KeyValue iWadFile;

    public event PropertyChangedEventHandler PropertyChanged;

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

    public ObservableCollection<NamePath> ModFiles
    {
        get; set;
    }

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
        Name = System.IO.Path.GetFileNameWithoutExtension(path);
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
