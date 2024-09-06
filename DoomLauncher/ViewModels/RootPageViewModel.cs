using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.WinUI.Collections;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Collections.Generic;
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
            SettingsViewModel.Current.SortOrder = value.Key;
            RefreshSort();
        }
    }

    private void RefreshSort()
    {
        using (Entries.DeferRefresh())
        {
            var order = SortOrder.Key;
            Entries.SortDescriptions.Clear();
            if (order == "launch")
            {
                Entries.SortDescriptions.Add(new SortDescription(nameof(DoomEntryViewModel.LongDescription), SortDirection.Descending));
            }
            else if (order == "created")
            {
                Entries.SortDescriptions.Add(new SortDescription(nameof(DoomEntryViewModel.Created), SortDirection.Descending));
            }
            else if (order == "playtime")
            {
                Entries.SortDescriptions.Add(new SortDescription(nameof(DoomEntryViewModel.PlayTime), SortDirection.Descending));
            }
            Entries.SortDescriptions.Add(new SortDescription(nameof(DoomEntryViewModel.Name), SortDirection.Ascending));
            Entries.SortDescriptions.Add(new SortDescription(nameof(DoomEntryViewModel.Created), SortDirection.Ascending));
        }
    }

    [ObservableProperty]
    private string searchQuery = "";

    public RootPageViewModel()
    {
        Entries.Filter = (object vm) => string.IsNullOrEmpty(SearchQuery) || ((DoomEntryViewModel)vm).Name.Contains(SearchQuery, System.StringComparison.CurrentCultureIgnoreCase);
        RefreshSort();
    }

    public AdvancedCollectionView Entries = new(SettingsViewModel.Current.Entries, true);

    partial void OnSearchQueryChanged(string value)
    {
        Entries.RefreshFilter();
    }
}
