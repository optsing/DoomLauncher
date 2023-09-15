using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Linq;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DoomLauncher;
/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class EditModContentDialog : ContentDialog
{
    private Settings settings;

    public EditModContentDialog(XamlRoot root, EditModDialogResult initial, Settings settings, EditDialogMode mode)
    {
        InitializeComponent();
        XamlRoot = root;
        ModName = initial.name;
        ModDescription = initial.description;
        ModLongDescription = initial.longDescription;
        this.settings = settings;
        UniqueConfig = initial.uniqueConfig;
        UniqueSavesFolder = initial.uniqueSavesFolder;

        GZDoomPackages = new List<GZDoomPackage>() { new GZDoomPackage { Path = "", Arch = AssetArch.notSelected } };
        GZDoomPackages.AddRange(settings.GZDoomInstalls);
        GZDoomPackage = settings.GZDoomInstalls.FirstOrDefault(package => package.Path == initial.gZDoomPath, GZDoomPackages.First());

        FilteredIWads = new() { new KeyValue("", "Не выбрано") };
        FilteredIWads.AddRange(settings.IWadFiles.Select(iWadFile => new KeyValue(iWadFile, Settings.IWads.GetValueOrDefault(iWadFile.ToLower(), iWadFile))));
        IWadFile = FilteredIWads.FirstOrDefault(iWad => iWad.Key == initial.iWadFile, FilteredIWads.First());

        PrimaryButtonText = mode switch
        {
            EditDialogMode.Create => "Создать",
            EditDialogMode.Edit => "Сохранить",
            EditDialogMode.Import => "Импортировать",
            _ => throw new System.NotImplementedException(),
        };
        Title = mode switch
        {
            EditDialogMode.Create => "Создание сборки",
            EditDialogMode.Edit => "Настройка сборки",
            EditDialogMode.Import => "Импорт сборки",
            _ => throw new System.NotImplementedException(),
        };
        ModFiles = initial.modFiles.Select(file => new TitleChecked(file)).ToList();
        ImageFiles = initial.imageFiles.Select(file => new TitleChecked(file)).ToList();
    }

    private void EditModContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (ModName == "")
        {
            tbModName.Focus(FocusState.Programmatic);
            args.Cancel = true;
        }
    }

    public string ModName
    {
        get; private set;
    }

    public string ModDescription
    {
        get; private set;
    }

    public string ModLongDescription
    {
        get; private set;
    }

    public List<GZDoomPackage> GZDoomPackages
    {
        get;
    }

    public GZDoomPackage GZDoomPackage
    {
        get; private set;
    }

    public List<KeyValue> FilteredIWads
    {
        get;
    }

    public KeyValue IWadFile
    {
        get; private set;
    }

    public bool UniqueConfig
    {
        get; private set;
    }

    public bool UniqueSavesFolder
    {
        get; private set;
    }

    public List<TitleChecked> ModFiles { get; private set; }
    public List<TitleChecked> ImageFiles { get; private set; }
}

public enum EditDialogMode
{
    Create, Edit, Import
}

public class EditModDialogResult
{
    public readonly string name;
    public readonly string description;
    public readonly string longDescription;
    public readonly string gZDoomPath;
    public readonly string iWadFile;
    public readonly bool uniqueConfig;
    public readonly bool uniqueSavesFolder;
    public readonly List<string> modFiles;
    public readonly List<string> imageFiles;

    public EditModDialogResult(DoomEntry entry, List<string>? modFiles = null, List<string>? imageFiles = null)
    {
        name = entry.Name;
        description = entry.Description;
        longDescription = entry.LongDescription;
        gZDoomPath = entry.GZDoomPath;
        iWadFile = entry.IWadFile;
        uniqueConfig = entry.UniqueConfig;
        uniqueSavesFolder = entry.UniqueSavesFolder;
        this.modFiles = modFiles ?? new List<string>();
        this.imageFiles = imageFiles ?? new List<string>();
    }
}

public class TitleChecked
{
    public string Title { get; set; }
    public bool IsChecked { get; set; }

    public TitleChecked(string title)
    {
        Title = title;
        IsChecked = true;
    }
}

public readonly struct KeyValue
{
    public readonly string Key;
    public readonly string Value;

    public KeyValue(string key, string value) { 
        Key = key;
        Value = value;
    }
}
