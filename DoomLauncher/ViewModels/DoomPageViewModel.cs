using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DoomLauncher.ViewModels;

public partial class DoomPageViewModel(DispatcherTimer timer) : ObservableObject
{
    private readonly DispatcherTimer timerSlideshow = timer;

    [ObservableProperty]
    private int currentTicksToSlideshow;

    [ObservableProperty]
    private bool isSlideshowEnabled;

    partial void OnIsSlideshowEnabledChanged(bool value)
    {
        if (value)
        {
            timerSlideshow.Start();
        }
        else
        {
            timerSlideshow.Stop();
        }
    }
}
