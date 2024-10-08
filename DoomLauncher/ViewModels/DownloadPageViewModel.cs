using CommunityToolkit.Mvvm.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoomLauncher.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
    public required List<string> Images { get; init; }
    public required string? Description { get; init; }
    public required string CurrentVersion { get; set; }
    public required List<string> Versions { get; init; }
    public required IRelayCommand DownloadCommand { get; init; }
}

public partial class DownloadPageViewModel : ObservableObject
{
    public ObservableGroupedCollection<string, DownloadEntryViewModel> Entries { get; } = [];

    [ObservableProperty]
    private Visibility hasNoItems = Visibility.Collapsed;

    public async void LoadEntries()
    {
        HasNoItems = Visibility.Collapsed;
        EventBus.Progress(this, Strings.Resources.ProgressLoadingOnlineEntries);
        var entries = await WebAPI.Current.DownloadEntriesFromJson(SettingsViewModel.Current.OnlineSource);
        EventBus.Progress(this, null);
        Entries.Clear();
        if (entries == null)
        {
            await DialogHelper.ShowAskAsync(
                Strings.Resources.DialogWrongOnlineEntriesUrlTitle,
                Strings.Resources.DialogWrongOnlineEntriesUrlText,
                Strings.Resources.DialogOKAction);
        }
        else 
        {
            var PortEntries = new ObservableGroup<string, DownloadEntryViewModel>(Strings.Resources.DownloadPageGroupPorts);
            var IWADEntries = new ObservableGroup<string, DownloadEntryViewModel>(Strings.Resources.DownloadPageGroupIWAD);
            var FileEntries = new ObservableGroup<string, DownloadEntryViewModel>(Strings.Resources.DownloadPageGroupFiles);
            foreach (var file in entries.Ports.OrderBy(p => p.Name))
            {
                var versions = file.Versions.Keys.ToList();
                Uri.TryCreate(file.Homepage, UriKind.Absolute, out var uri);
                PortEntries.Add(new()
                {
                    Source = file,
                    Name = file.Name,
                    Description = file.Description,
                    Homepage = uri,
                    Images = file.Images,
                    CurrentVersion = versions[0],
                    Versions = versions,
                    DownloadCommand = DownloadEntryCommand,
                });
            }
            foreach (var file in entries.IWAD.OrderBy(p => p.Name))
            {
                var versions = file.Versions.Keys.ToList();
                Uri.TryCreate(file.Homepage, UriKind.Absolute, out var uri);
                IWADEntries.Add(new()
                {
                    Source = file,
                    Name = file.Name,
                    Description = file.Description,
                    Homepage = uri,
                    Images = file.Images,
                    CurrentVersion = versions[0],
                    Versions = versions,
                    DownloadCommand = DownloadEntryCommand,
                });
            }
            foreach (var file in entries.Files.OrderBy(p => p.Name))
            {
                var versions = file.Versions.Keys.ToList();
                Uri.TryCreate(file.Homepage, UriKind.Absolute, out var uri);
                FileEntries.Add(new()
                {
                    Source = file,
                    Name = file.Name,
                    Description = file.Description,
                    Homepage = uri,
                    Images = file.Images,
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
        if (Entries.Count == 0)
        {
            HasNoItems = Visibility.Visible;
        }
    }

    [RelayCommand]
    private async Task DownloadEntry(DownloadEntryViewModel vm)
    {
        var version = vm.CurrentVersion;
        if (vm.Source is DownloadEntry entry)
        {
            
            var result = await DialogHelper.ShowAskAsync(
                Strings.Resources.DialogDownloadEntryTitle(vm.Name),
                Strings.Resources.DialogCreateOrDownloadEntryText(vm.Name, version),
                Strings.Resources.DialogDownloadAndCreateAction,
                Strings.Resources.DialogDownloadOnlyAction,
                Strings.Resources.DialogCancelAction);
            if (result != ContentDialogResult.None)
            {
                try
                {
                    var entries = await DownloadEntryHelper.InstallEntry(entry, version, result == ContentDialogResult.Primary, progress => EventBus.Progress(this, progress));
                    foreach (var item in entries)
                    {
                        SettingsViewModel.Current.Entries.Add(item);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                    await DialogHelper.ShowAskAsync(
                        Strings.Resources.DialogDownloadErrorTitle,
                        Strings.Resources.DialogDownloadErrorText,
                        Strings.Resources.DialogOKAction);
                }
            }
        }
        else if (vm.Source is DownloadPort port)
        {
            if (await DialogHelper.ShowAskAsync(
                Strings.Resources.DialogDownloadEntryTitle(vm.Name),
                Strings.Resources.DialogDownloadEntryText(vm.Name, version),
                Strings.Resources.DialogDownloadAction,
                Strings.Resources.DialogCancelAction)
            )
            {
                try
                {
                    await DownloadEntryHelper.InstallPort(port, version, progress => EventBus.Progress(this, progress));
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                    await DialogHelper.ShowAskAsync(
                        Strings.Resources.DialogDownloadErrorTitle,
                        Strings.Resources.DialogDownloadErrorText,
                        Strings.Resources.DialogOKAction);
                }
            }
        }
    }
}
