﻿using DoomLauncher.Helpers;
using DoomLauncher.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DoomLauncher;

public sealed partial class SettingsPage : Page
{

    public SettingsPageViewModel ViewModel = new();

    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        EventBus.ChangeBackground(null, AnimationDirection.None);
        EventBus.ChangeCaption("Settings");
        base.OnNavigatedTo(e);
    }
}
