using CommunityToolkit.Mvvm.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoomLauncher.Helpers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DoomLauncher.ViewModels;

public partial class DownloadEntryViewModel
{
    public required DownloadEntry Source { get; init; }
    public required string Name { get; init; }
    public required DownloadEntryType Type { get; init; }
    public required string? Description { get; init; }
    public required string CurrentVersion { get; set; }
    public required List<string> Versions { get; init; }
    public required IRelayCommand DownloadCommand { get; init; }
}

public partial class DownloadPageViewModel : ObservableObject
{
    public ObservableGroupedCollection<string, DownloadEntryViewModel> Entries { get; } = [];

    public async void LoadEntries()
    {
        var entries = await WebAPI.Current.DownloadEntriesFromGitHub();
        if (entries != null)
        {
            var IWADEntries = new ObservableGroup<string, DownloadEntryViewModel>(Strings.Resources.DownloadPageGroupIWAD);
            var FileEntries = new ObservableGroup<string, DownloadEntryViewModel>(Strings.Resources.DownloadPageGroupFiles);
            foreach (var file in entries.IWADs)
            {
                var versions = file.Versions.Keys.ToList();
                IWADEntries.Add(new()
                {
                    Source = file,
                    Name = file.Name,
                    Type = DownloadEntryType.IWAD,
                    Description = file.Description,
                    CurrentVersion = versions[0],
                    Versions = versions,
                    DownloadCommand = DownloadEntryCommand,
                });
            }
            foreach (var file in entries.Files)
            {
                var versions = file.Versions.Keys.ToList();
                FileEntries.Add(new()
                {
                    Source = file,
                    Name = file.Name,
                    Type = DownloadEntryType.File,
                    Description = file.Description,
                    CurrentVersion = versions[0],
                    Versions = versions,
                    DownloadCommand = DownloadEntryCommand,
                });
            }
            Entries.Add(IWADEntries);
            Entries.Add(FileEntries);
        }
    }

    [RelayCommand]
    private async Task DownloadEntry(DownloadEntryViewModel vm)
    {
        var entry = vm.Source;
        var version = vm.CurrentVersion;
        if (await DialogHelper.ShowAskAsync(
            Strings.Resources.DialogDownloadEntryTitle(entry.Name),
            Strings.Resources.DialogDownloadEntryText(entry.Name, version),
            Strings.Resources.DialogDownloadAction,
            Strings.Resources.DialogCancelAction)
        )
        {
            await DownloadEntryHelper.InstallEntry(entry, version, vm.Type, progress => EventBus.Progress(this, progress));
        }
    }
}
