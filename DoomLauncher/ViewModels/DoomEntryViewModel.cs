using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace DoomLauncher.ViewModels;

public partial class DoomEntryViewModel : ObservableObject
{
    [ObservableProperty]
    public required partial string Name { get; set; } = "";

    [ObservableProperty]
    public partial string Description { get; set; } = "";

    [ObservableProperty]
    public partial string LongDescription { get; set; } = "";

    [ObservableProperty]
    public partial string GZDoomPath { get; set; } = "";

    [ObservableProperty]
    public partial string IWadFile { get; set; } = "";

    [ObservableProperty]
    public partial string SteamGame { get; set; } = "";

    [ObservableProperty]
    public partial bool UniqueConfig { get; set; } = false;
    [ObservableProperty]
    public partial bool UniqueSavesFolder { get; set; } = false;

    public required string Id { get; set; } = "";

    [ObservableProperty]
    public partial int SelectedImageIndex { get; set; } = 0;

    public bool Slideshow { get; set; } = true;

    public List<string> ImageFiles { get; set; } = [];
    public List<string> ModFiles { get; set; } = [];

    [ObservableProperty]
    public required partial DateTime? Created { get; set; } = null;

    [ObservableProperty]
    public partial DateTime? LastLaunch { get; set; } = null;

    [ObservableProperty]
    public partial TimeSpan? PlayTime { get; set; }
}

