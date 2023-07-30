﻿using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Windows.Storage;

namespace DoomLauncher;

public class Settings
{
    public string GZDoomPath { get; set; } = "";
    public bool CloseOnLaunch { get; set; } = false;
    public int SelectedModIndex { get; set; } = 0;
    public ObservableCollection<DoomEntry> Entries { get; set; } = new();

    public int? WindowX { get; set; } = null;
    public int? WindowY { get; set; } = null;
    public int? WindowWidth { get; set; } = null;
    public int? WindowHeight { get; set; } = null;
    public bool WindowMaximized { get; set; } = false;

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

    public static readonly string[] SupportedModExtensions = new[] { ".pk3", ".wad", ".zip" };
    public static readonly string[] SupportedImageExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif", ".svg" };

    public static readonly JsonSerializerOptions jsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
        Converters = {
            new NamePathConverter(),
            new KeyValueConverter(),
        },
    };

    public static async Task<string> CopyFileWithConfirmation(XamlRoot xamlRoot, StorageFile file, string targetFolder)
    {
        var targetPath = Path.Combine(targetFolder, file.Name);
        if (targetPath != file.Path)
        {
            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }
            if (!File.Exists(targetPath) || await AskDialog.ShowAsync(
                xamlRoot,
                "Добавление с заменой",
                $"Файл '{file.Name}' уже существует в папке лаунчера.\nЗаменить?",
                "Заменить",
                "Не заменять"
            ))
            {
                using var sourceStream = await file.OpenStreamForReadAsync();
                using var destinationStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write);
                await sourceStream.CopyToAsync(destinationStream);
            }
        }
        return targetPath;
    }

    public static async Task<string> CopyFileWithConfirmation(XamlRoot xamlRoot, Stream sourceStream, string fileName, string targetFolder)
    {
        var targetPath = Path.Combine(targetFolder, fileName);
        if (!Directory.Exists(targetFolder))
        {
            Directory.CreateDirectory(targetFolder);
        }
        if (!File.Exists(targetPath) || await AskDialog.ShowAsync(
            xamlRoot,
            "Добавление с заменой",
            $"Файл '{fileName}' уже существует в папке лаунчера.\nЗаменить?",
            "Заменить",
            "Не заменять"
        ))
        {
            using var destinationStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write);
            await sourceStream.CopyToAsync(destinationStream);
        }
        return targetPath;
    }
}


public partial class DoomEntry: ObservableObject
{
    [ObservableProperty]
    private string name = "";
    [ObservableProperty]
    private string description = "";
    [ObservableProperty]
    private KeyValue iWadFile = Settings.IWads.First();
    [ObservableProperty]
    private bool uniqueConfig = false;

    public string Id { get; set; }

    public ObservableCollection<string> ImageFiles { get; set; } = new();
    public ObservableCollection<NamePath> ModFiles { get; set; } = new();
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
