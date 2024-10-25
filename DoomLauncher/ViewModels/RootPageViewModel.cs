using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoomLauncher.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace DoomLauncher.ViewModels;
public partial class RootPageViewModel : ObservableObject
{
    [ObservableProperty]
    private string? caption = null;

    [ObservableProperty]
    private BitmapImage? background = null;

    [ObservableProperty]
    private DoomEntryViewModel? currentEntry = null;

    [ObservableProperty]
    private string? progressText = null;

    [ObservableProperty]
    private bool isLeftDropHelperVisible;

    [ObservableProperty]
    private bool isRightDropHelperVisible;

    public List<KeyValue> SortOrders => [
        new("", Strings.Resources.SortOrderByName),
        new("launch", Strings.Resources.SortOrderByLaunch),
        new("created", Strings.Resources.SortOrderByCreated),
        new("playtime", Strings.Resources.SortOrderByPlayTime),
    ];
        
    public KeyValue SortOrder {
        get
        {
            return SortOrders.FirstOrDefault(so => so.Key == SettingsViewModel.Current.SortOrder, SortOrders[0]);
        }
        set
        {
            if (SettingsViewModel.Current.SortOrder != value.Key)
            {
                SettingsViewModel.Current.SortOrder = value.Key;
                RefreshSort();
                SyncCollection.SyncImmediate();
            }
        }
    }

    private void RefreshSort()
    {
        var order = SortOrder.Key;
        SyncCollection.ClearSort();
        if (order == "launch")
        {
            SyncCollection.AddSort(nameof(DoomEntryViewModel.LastLaunch), x => x.LastLaunch, true);
        }
        else if (order == "created")
        {
            SyncCollection.AddSort(nameof(DoomEntryViewModel.Created), x => x.Created, true);
        }
        else if (order == "playtime")
        {
            SyncCollection.AddSort(nameof(DoomEntryViewModel.PlayTime), x => x.PlayTime, true);
        }
        SyncCollection.AddSort(nameof(DoomEntryViewModel.Name), x => x.Name);
        SyncCollection.AddSort(nameof(DoomEntryViewModel.Created), x => x.Created);
    }

    [ObservableProperty]
    private string searchQuery = "";

    public RootPageViewModel()
    {
        SyncCollection = new SyncCollection<DoomEntryViewModel>(SettingsViewModel.Current.Entries, Entries)
        {
            Filter = vm => string.IsNullOrEmpty(SearchQuery) || vm.Name.Contains(SearchQuery, System.StringComparison.CurrentCultureIgnoreCase),
        };
        RefreshSort();
        SyncCollection.SyncImmediate();
    }

    public ObservableCollection<DoomEntryViewModel> Entries { get; } =[];
    private readonly SyncCollection<DoomEntryViewModel> SyncCollection;

    partial void OnSearchQueryChanged(string value)
    {
        SyncCollection.SyncDebounce();
    }

    public void AddEntries(List<DoomEntryViewModel> entries)
    {
        if (entries.Count > 0)
        {
            foreach (var entry in entries)
            {
                SettingsViewModel.Current.Entries.Add(entry);
            }
            EventBus.SetCurrentEntry(entries.Last());
            SettingsViewModel.Current.Save();
        }
    }

    [RelayCommand]
    public async Task EditEntry(DoomEntryViewModel? entry)
    {
        if (entry == null)
        {
            return;
        }
        var result = await DialogHelper.ShowEditEntryAsync(entry, EditDialogMode.Edit);
        if (result.Result == ContentDialogResult.Primary)
        {
            result.ViewModel.UpdateEntry(entry);
            SettingsViewModel.Current.Save();
            await JumpListHelper.Update();
        }
        else if (result.Result == ContentDialogResult.Secondary)
        {
            var newEntry = new DoomEntryViewModel()
            {
                Id = Guid.NewGuid().ToString(),
                Name = "",
                Created = DateTime.Now,
                SelectedImageIndex = entry.SelectedImageIndex,
                ModFiles = new(entry.ModFiles),
                ImageFiles = new(entry.ImageFiles),
            };
            result.ViewModel.UpdateEntry(newEntry);
            AddEntries([newEntry]);
        }
    }

    [RelayCommand]
    private async Task CreateShortcutEntry(DoomEntryViewModel? entry)
    {
        if (entry == null)
        {
            return;
        }
        var picker = new Windows.Storage.Pickers.FileSavePicker();

        // Need to initialize the picker object with the hwnd / IInitializeWithWindow
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinApi.HWND);

        // Now we can use the picker object as normal
        picker.FileTypeChoices.Add(Strings.Resources.FileTypeShortcutTitle, [".url"]);
        picker.SuggestedFileName = entry.Name;
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
        picker.CommitButtonText = Strings.Resources.CreateShortcut;

        var file = await picker.PickSaveFileAsync();
        if (file == null)
        {
            return;
        }

        EventBus.Progress(Strings.Resources.ProgressCreatingShortcut);
        await FileHelper.CreateEntryShortcut(entry, file);
        EventBus.Progress(null);
    }

    [RelayCommand]
    private async Task ExportEntry(DoomEntryViewModel? entry)
    {
        if (entry == null)
        {
            return;
        }
        var picker = new Windows.Storage.Pickers.FileSavePicker();

        // Need to initialize the picker object with the hwnd / IInitializeWithWindow
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinApi.HWND);

        // Now we can use the picker object as normal
        picker.FileTypeChoices.Add(Strings.Resources.FileTypeGZDLTitle, [".gzdl"]);
        picker.SuggestedFileName = entry.Name;
        picker.CommitButtonText = Strings.Resources.Export;

        var file = await picker.PickSaveFileAsync();
        if (file != null)
        {
            await EntryHelper.ExportToGZDLFile(entry, file, progress => EventBus.Progress(progress));
        }
    }

    [RelayCommand]
    private async Task RemoveEntry(DoomEntryViewModel? entry)
    {
        if (entry == null)
        {
            return;
        }
        if (await DialogHelper.ShowAskAsync(Strings.Resources.DialogEntryRemoveTitle, Strings.Resources.DialogEntryRemoveText(entry.Name), Strings.Resources.DialogRemoveAction, Strings.Resources.DialogCancelAction))
        {
            if (entry == CurrentEntry)
            {
                EventBus.SetCurrentEntry(null);
            }
            SettingsViewModel.Current.Entries.Remove(entry);
            SettingsViewModel.Current.Save();
            await JumpListHelper.Update();
        }
    }


    public void LaunchEntryById(string entryId, bool forceClose)
    {
        var entry = SettingsViewModel.Current.Entries.FirstOrDefault(entry => string.Equals(entry.Id, entryId));
        LaunchEntry(entry, forceClose);
    }

    public void LaunchEntryByName(string entryName, bool forceClose)
    {
        var entry = SettingsViewModel.Current.Entries.FirstOrDefault(entry => string.Equals(entry.Name, entryName));
        LaunchEntry(entry, forceClose);
    }

    [RelayCommand]
    private void LaunchEntry(DoomEntryViewModel? entry)
    {
        LaunchEntry(entry, false);
    }

    private async void LaunchEntry(DoomEntryViewModel? entry, bool forceClose)
    {
        if (entry == null)
        {
            await DialogHelper.ShowAskAsync(Strings.Resources.DialogLaunchErrorTitle, Strings.Resources.DialogLaunchErrorNoEntryText, Strings.Resources.DialogOKAction);
            return;
        }
        EventBus.SetCurrentEntry(entry);
        var result = LaunchHelper.LaunchEntry(entry);
        if (result == LaunchResult.Success && LaunchHelper.CurrentProcess != null)
        {
            entry.LastLaunch = DateTime.Now;
            await JumpListHelper.Update();
            if (SettingsViewModel.Current.CloseOnLaunch || forceClose)
            {
                Application.Current.Exit();
            }
            else
            {
                await LaunchHelper.CurrentProcess.WaitForExitAsync();
                entry.PlayTime = (entry.PlayTime ?? new()) + (LaunchHelper.CurrentProcess.ExitTime - LaunchHelper.CurrentProcess.StartTime);
                if (LaunchHelper.CurrentProcess.ExitCode != 0)
                {
                    var error = await LaunchHelper.CurrentProcess.StandardError.ReadToEndAsync();
                    if (string.IsNullOrEmpty(error))
                    {
                        await DialogHelper.ShowAskAsync(Strings.Resources.DialogLaunchErrorTitle, Strings.Resources.DialogLaunchErrorCodeText(LaunchHelper.CurrentProcess.ExitCode), Strings.Resources.DialogOKAction);
                    }
                    else
                    {
                        await DialogHelper.ShowAskAsync(Strings.Resources.DialogLaunchErrorTitle, Strings.Resources.DialogLaunchErrorText(error), Strings.Resources.DialogOKAction);
                    }
                }
            }
        }
        else if (result == LaunchResult.AlreadyLaunched && LaunchHelper.CurrentProcess != null)
        {
            await DialogHelper.ShowAskAsync(Strings.Resources.DialogLaunchErrorTitle, Strings.Resources.DialogLaunchErrorAlreadyLaunchedText, Strings.Resources.DialogOKAction);
        }
        else if (result == LaunchResult.PathNotValid)
        {
            if (await DialogHelper.ShowAskAsync(Strings.Resources.DialogLaunchErrorTitle, Strings.Resources.DialogLaunchErrorGZDoomPathNotValidText, Strings.Resources.DialogEditAction, Strings.Resources.DialogCancelAction))
            {
                await EditEntry(entry);
            }
        }
        else
        {
            await DialogHelper.ShowAskAsync(Strings.Resources.DialogLaunchErrorTitle, Strings.Resources.DialogLaunchErrorUnknownText, Strings.Resources.DialogOKAction);
        }
    }
}
