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
    public EditModContentDialog(XamlRoot root, EditModDialogResult initial, List<KeyValue> filteredIWads, EditDialogMode mode)
    {
        InitializeComponent();
        XamlRoot = root;
        ModName = initial.name;
        ModDescription = initial.description;
        ModLongDescription = initial.longDescription;
        IWadFile = filteredIWads.FirstOrDefault(kv => kv.Key == initial.iWadFile, filteredIWads.First());
        UniqueConfig = initial.uniqueConfig;
        UniqueSavesFolder = initial.uniqueSavesFolder;
        FilteredIWads = filteredIWads;
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

    public List<KeyValue> FilteredIWads {
        get;
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
        this.Key = key;
        this.Value = value;
    }
}