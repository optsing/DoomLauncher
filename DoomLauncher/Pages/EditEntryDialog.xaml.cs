using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DoomLauncher;

public class EditEntryDialogViewModel(EditDialogMode mode)
{
    private static readonly DoomPackageViewModel DefaultDoomPackage = new() { Path = "", Arch = AssetArch.notSelected };
    private static readonly KeyValue DefaultIWadFile = new("", "По умолчанию");
    private static readonly KeyValue DefaultSteamGame = new("", "По умолчанию");

    private readonly EditDialogMode mode = mode;
    public string Title => mode switch
    {
        EditDialogMode.Create => "Создание сборки",
        EditDialogMode.Edit => "Настройка сборки",
        EditDialogMode.Import => "Импорт сборки",
        EditDialogMode.Copy => "Дублирование сборки",
        _ => throw new NotImplementedException(),
    };
    public string PrimaryButtonText => mode switch
    {
        EditDialogMode.Create => "Создать",
        EditDialogMode.Edit => "Сохранить",
        EditDialogMode.Import => "Импортировать",
        EditDialogMode.Copy => "Дублировать",
        _ => throw new NotImplementedException(),
    };

    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string LongDescription { get; set; } = "";
    public bool UniqueConfig { get; set; } = false;
    public bool UniqueSavesFolder { get; set; } = false;

    public List<DoomPackageViewModel> DoomPackages => [DefaultDoomPackage, .. Settings.Current.GZDoomInstalls];
    public List<KeyValue> IWadFiles =>
        [
            DefaultIWadFile,
            .. Settings.Current.IWadFiles.Select(iWadFile => new KeyValue(iWadFile, FileHelper.IWadFileToTitle(iWadFile))),
        ];
    public List<KeyValue> SteamGames => [DefaultSteamGame, .. FileHelper.SteamAppIds.Select(item => new KeyValue(item.Key, item.Value.title))];

    public DoomPackageViewModel DoomPackage { get; set; } = DefaultDoomPackage;
    public KeyValue IWadFile { get; set; } = DefaultIWadFile;
    public KeyValue SteamGame { get; set; } = DefaultSteamGame;

    public List<TitleChecked> ModFiles { get; private set; } = [];
    public List<TitleChecked> ImageFiles { get; private set; } = [];

    public static EditEntryDialogViewModel FromEntry(DoomEntry entry, EditDialogMode mode, List<string>? modFiles = null, List<string>? imageFiles = null)
    {
        var vm = new EditEntryDialogViewModel(mode)
        {
            Name = entry.Name,
            Description = entry.Description,
            LongDescription = entry.LongDescription,
            UniqueConfig = entry.UniqueConfig,
            UniqueSavesFolder = entry.UniqueSavesFolder,
            ModFiles = modFiles?.Select(file => new TitleChecked(file)).ToList() ?? [],
            ImageFiles = imageFiles?.Select(file => new TitleChecked(file)).ToList() ?? [],
        };
        vm.DoomPackage = vm.DoomPackages.FirstOrDefault(package => package.Path == entry.GZDoomPath, DefaultDoomPackage);
        vm.IWadFile = vm.IWadFiles.FirstOrDefault(iWad => iWad.Key == entry.IWadFile, DefaultIWadFile);
        vm.SteamGame = vm.SteamGames.FirstOrDefault(steamGame => steamGame.Key == entry.SteamGame, DefaultSteamGame);
        return vm;
    }

    public List<string> GetModFiles() => ModFiles.Where(tc => tc.IsChecked).Select(tc => tc.Title).ToList();
    public List<string> GetImageFiles() => ImageFiles.Where(tc => tc.IsChecked).Select(tc => tc.Title).ToList();

    public void UpdateEntry(DoomEntry entry)
    {
        entry.Name = Name.Trim();
        entry.Description = Description.Trim();
        entry.LongDescription = LongDescription.Trim();
        entry.GZDoomPath = DoomPackage.Path;
        entry.IWadFile = IWadFile.Key;
        entry.SteamGame = SteamGame.Key;
        entry.UniqueConfig = UniqueConfig;
        entry.UniqueSavesFolder = UniqueSavesFolder;
    }
}


/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class EditEntryDialog : ContentDialog
{
    public EditEntryDialogViewModel ViewModel { get; set; }

    public EditEntryDialog(EditEntryDialogViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
    }

    private void EditEntryDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.Name))
        {
            tbModName.Focus(FocusState.Programmatic);
            args.Cancel = true;
        }
    }
}

public enum EditDialogMode
{
    Create, Edit, Import, Copy
}

public class TitleChecked(string title)
{
    public string Title { get; set; } = title;
    public bool IsChecked { get; set; } = true;
}

public readonly struct KeyValue(string key, string value)
{
    public readonly string Key = key;
    public readonly string Value = value;
}
