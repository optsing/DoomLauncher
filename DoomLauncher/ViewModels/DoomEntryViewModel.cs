using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace DoomLauncher.ViewModels;

public partial class DoomEntryViewModel : ObservableObject
{
    public required string Name { get => name; set => SetProperty(ref name, value); }
    private string name = "";

    public string Description { get => description; set => SetProperty(ref description, value); }
    private string description = "";

    public string LongDescription { get => longDescription; set => SetProperty(ref longDescription, value); }
    private string longDescription = "";

    public string GZDoomPath { get => gZDoomPath; set => SetProperty(ref gZDoomPath, value); }
    private string gZDoomPath = "";

    public string IWadFile { get => iWadFile; set => SetProperty(ref iWadFile, value); }
    private string iWadFile = "";

    public string SteamGame { get => steamGame; set => SetProperty(ref steamGame, value); }
    private string steamGame = "";

    public bool UniqueConfig { get => uniqueConfig; set => SetProperty(ref uniqueConfig, value); }
    private bool uniqueConfig = false;
    public bool UniqueSavesFolder { get => uniqueSavesFolder; set => SetProperty(ref uniqueSavesFolder, value); }
    private bool uniqueSavesFolder = false;

    public required string Id { get; set; } = "";

    public int SelectedImageIndex { get => selectedImageIndex; set => SetProperty(ref selectedImageIndex, value); }
    private int selectedImageIndex = 0;

    public bool Slideshow { get; set; } = true;

    public List<string> ImageFiles { get; set; } = [];
    public List<string> ModFiles { get; set; } = [];

    public required DateTime? Created { get => created; set => SetProperty(ref created, value); }
    private DateTime? created = null;

    public DateTime? LastLaunch { get => lastLaunch; set => SetProperty(ref lastLaunch, value); }
    private DateTime? lastLaunch = null;

    public TimeSpan? PlayTime { get => playTime; set => SetProperty(ref playTime, value); }
    private TimeSpan? playTime;
}

