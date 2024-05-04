using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;

namespace DoomLauncher.ViewModels;

public partial class DoomEntryViewModel : ObservableObject
{
    [ObservableProperty]
    private string name = "";

    [ObservableProperty]
    private string description = "";

    [ObservableProperty]
    private string longDescription = "";

    [ObservableProperty]
    private string gZDoomPath = "";

    [ObservableProperty]
    private string iWadFile = "";

    public string SteamGame { get; set; } = "";
    public bool UniqueConfig { get; set; } = false;
    public bool UniqueSavesFolder { get; set; } = false;

    public string Id { get; set; } = "";

    public int SelectedImageIndex { get; set; } = 0;

    public bool Slideshow { get; set; } = true;

    public ObservableCollection<string> ImageFiles { get; set; } = [];
    public ObservableCollection<string> ModFiles { get; set; } = [];

    [ObservableProperty]
    private DateTime? created = null;

    [ObservableProperty]
    private DateTime? lastLaunch = null;

    [ObservableProperty]
    private TimeSpan? playTime;
}

