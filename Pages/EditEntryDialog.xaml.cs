using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DoomLauncher;
/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class EditEntryDialog : ContentDialog
{
    public EditEntryDialog(XamlRoot xamlRoot, EditEntryDialogResult initial, EditDialogMode mode)
    {
        InitializeComponent();
        XamlRoot = xamlRoot;
        ModName = initial.name;
        ModDescription = initial.description;
        ModLongDescription = initial.longDescription;
        UniqueConfig = initial.uniqueConfig;
        UniqueSavesFolder = initial.uniqueSavesFolder;

        GZDoomPackages = new List<GZDoomPackage>() { new GZDoomPackage { Path = "", Arch = AssetArch.notSelected } };
        GZDoomPackages.AddRange(Settings.Current.GZDoomInstalls);
        GZDoomPackage = Settings.Current.GZDoomInstalls.FirstOrDefault(package => package.Path == initial.gZDoomPath, GZDoomPackages.First());

        IWadFiles = new() { new KeyValue("", "Не выбрано") };
        IWadFiles.AddRange(Settings.Current.IWadFiles.Select(iWadFile => new KeyValue(iWadFile, FileHelper.GetIWadTitle(iWadFile))));
        IWadFile = IWadFiles.FirstOrDefault(iWad => iWad.Key == initial.iWadFile, IWadFiles.First());

        PrimaryButtonText = mode switch
        {
            EditDialogMode.Create => "Создать",
            EditDialogMode.Edit => "Сохранить",
            EditDialogMode.Import => "Импортировать",
            _ => throw new NotImplementedException(),
        };
        Title = mode switch
        {
            EditDialogMode.Create => "Создание сборки",
            EditDialogMode.Edit => "Настройка сборки",
            EditDialogMode.Import => "Импорт сборки",
            _ => throw new NotImplementedException(),
        };
        ModFiles = initial.modFiles.Select(file => new TitleChecked(file)).ToList();
        ImageFiles = initial.imageFiles.Select(file => new TitleChecked(file)).ToList();
    }

    private void EditEntryDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
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

    public List<KeyValue> IWadFiles
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

    public static async Task<EditEntryDialogResult?> ShowAsync(XamlRoot xamlRoot, EditEntryDialogResult initial, EditDialogMode mode)
    {
        var dialog = new EditEntryDialog(xamlRoot, initial, mode);
        if (ContentDialogResult.Primary == await dialog.ShowAsync())
        {
            var modFiles = dialog.ModFiles.Where(tc => tc.IsChecked).Select(tc => tc.Title).ToList();
            var imageFiles = dialog.ImageFiles.Where(tc => tc.IsChecked).Select(tc => tc.Title).ToList();
            return new EditEntryDialogResult(
                new DoomEntry()
                {
                    Name = dialog.ModName,
                    Description = dialog.ModDescription,
                    LongDescription = dialog.ModLongDescription,
                    GZDoomPath = dialog.GZDoomPackage.Path,
                    IWadFile = dialog.IWadFile.Key,
                    UniqueConfig = dialog.UniqueConfig,
                    UniqueSavesFolder = dialog.UniqueSavesFolder,
                }, modFiles, imageFiles);
        }
        return null;
    }
}

public enum EditDialogMode
{
    Create, Edit, Import
}

public class EditEntryDialogResult
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

    public EditEntryDialogResult(DoomEntry entry, List<string>? modFiles = null, List<string>? imageFiles = null)
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
