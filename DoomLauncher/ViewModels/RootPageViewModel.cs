using CommunityToolkit.Mvvm.ComponentModel;
using DoomLauncher.Helpers;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

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
}
