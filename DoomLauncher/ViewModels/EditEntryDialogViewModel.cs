using System;
using System.Collections.Generic;
using System.Linq;

namespace DoomLauncher.ViewModels;

public class EditEntryDialogViewModel(EditDialogMode mode)
{
    private static readonly DoomPackageViewModel DefaultDoomPackage = new() { Path = "", Arch = AssetArch.notSelected };
    private static readonly KeyValue DefaultIWadFile = new("", Strings.Resources.DefaultValue);
    private static readonly KeyValue DefaultSteamGame = new("", Strings.Resources.DefaultValue);

    private readonly EditDialogMode mode = mode;
    public string Title => mode switch
    {
        EditDialogMode.Create => Strings.Resources.EditDialogModeCreateTitle,
        EditDialogMode.Edit => Strings.Resources.EditDialogModeEditTitle,
        EditDialogMode.Import => Strings.Resources.EditDialogModeImportTitle,
        EditDialogMode.Copy => Strings.Resources.EditDialogModeCopyTitle,
        _ => throw new NotImplementedException(),
    };
    public string PrimaryButtonText => mode switch
    {
        EditDialogMode.Create => Strings.Resources.EditDialogModeCreateAction,
        EditDialogMode.Edit => Strings.Resources.EditDialogModeEditAction,
        EditDialogMode.Import => Strings.Resources.EditDialogModeImportAction,
        EditDialogMode.Copy => Strings.Resources.EditDialogModeCopyAction,
        _ => throw new NotImplementedException(),
    };

    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string LongDescription { get; set; } = "";
    public bool UniqueConfig { get; set; } = false;
    public bool UniqueSavesFolder { get; set; } = false;

    public List<DoomPackageViewModel> DoomPackages => [DefaultDoomPackage, .. SettingsViewModel.Current.GZDoomInstalls];
    public List<KeyValue> IWadFiles =>
        [
            DefaultIWadFile,
            .. SettingsViewModel.Current.IWadFiles.Select(iWadFile => new KeyValue(iWadFile, FileHelper.IWadFileToTitle(iWadFile))),
        ];
    public List<KeyValue> SteamGames => [DefaultSteamGame, .. FileHelper.SteamAppIds.Select(item => new KeyValue(item.Key, item.Value.title))];

    public DoomPackageViewModel DoomPackage { get; set; } = DefaultDoomPackage;
    public KeyValue IWadFile { get; set; } = DefaultIWadFile;
    public KeyValue SteamGame { get; set; } = DefaultSteamGame;

    public List<TitleChecked> ModFiles { get; private set; } = [];
    public List<TitleChecked> ImageFiles { get; private set; } = [];

    public static EditEntryDialogViewModel FromEntry(DoomEntryViewModel entry, EditDialogMode mode, List<string>? modFiles = null, List<string>? imageFiles = null)
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

    public void UpdateEntry(DoomEntryViewModel entry)
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


