using CommunityToolkit.Mvvm.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoomLauncher.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DoomLauncher.ViewModels;

public partial class DownloadEntryViewModel
{
    public required object Source { get; init; }
    public required string Name { get; init; }
    public required Uri? Homepage { get; init; }
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
            var PortEntries = new ObservableGroup<string, DownloadEntryViewModel>(Strings.Resources.DownloadPageGroupPorts);
            var IWADEntries = new ObservableGroup<string, DownloadEntryViewModel>(Strings.Resources.DownloadPageGroupIWAD);
            var FileEntries = new ObservableGroup<string, DownloadEntryViewModel>(Strings.Resources.DownloadPageGroupFiles);
            foreach (var file in entries.Ports)
            {
                var versions = file.Versions.Keys.ToList();
                Uri.TryCreate(file.Homepage, UriKind.Absolute, out var uri);
                PortEntries.Add(new()
                {
                    Source = file,
                    Name = file.Name,
                    Type = DownloadEntryType.IWAD,
                    Description = file.Description,
                    Homepage = uri,
                    CurrentVersion = versions[0],
                    Versions = versions,
                    DownloadCommand = DownloadEntryCommand,
                });
            }
            foreach (var file in entries.IWADs)
            {
                var versions = file.Versions.Keys.ToList();
                Uri.TryCreate(file.Homepage, UriKind.Absolute, out var uri);
                IWADEntries.Add(new()
                {
                    Source = file,
                    Name = file.Name,
                    Type = DownloadEntryType.IWAD,
                    Description = file.Description,
                    Homepage = uri,
                    CurrentVersion = versions[0],
                    Versions = versions,
                    DownloadCommand = DownloadEntryCommand,
                });
            }
            foreach (var file in entries.Files)
            {
                var versions = file.Versions.Keys.ToList();
                Uri.TryCreate(file.Homepage, UriKind.Absolute, out var uri);
                FileEntries.Add(new()
                {
                    Source = file,
                    Name = file.Name,
                    Type = DownloadEntryType.File,
                    Description = file.Description,
                    Homepage = uri,
                    CurrentVersion = versions[0],
                    Versions = versions,
                    DownloadCommand = DownloadEntryCommand,
                });
            }
            if (PortEntries.Count > 0)
            {
                Entries.Add(PortEntries);
            }
            if (IWADEntries.Count > 0)
            {
                Entries.Add(IWADEntries);
            }
            if (FileEntries.Count > 0)
            {
                Entries.Add(FileEntries);
            }
        }
    }

    [RelayCommand]
    private async Task DownloadEntry(DownloadEntryViewModel vm)
    {
        var version = vm.CurrentVersion;
        if (await DialogHelper.ShowAskAsync(
            Strings.Resources.DialogDownloadEntryTitle(vm.Name),
            Strings.Resources.DialogDownloadEntryText(vm.Name, version),
            Strings.Resources.DialogDownloadAction,
            Strings.Resources.DialogCancelAction)
        )
        {
            if (vm.Source is DownloadEntry entry)
            {
                await DownloadEntryHelper.InstallEntry(entry, version, vm.Type, progress => EventBus.Progress(this, progress));
            }
            else if (vm.Source is DownloadPort port)
            {
                await DownloadEntryHelper.InstallPort(port, version, progress => EventBus.Progress(this, progress));
            }
        }
    }
}
