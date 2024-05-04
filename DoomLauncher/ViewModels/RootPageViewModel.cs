using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;

namespace DoomLauncher.ViewModels;
public partial class RootPageViewModel : ObservableObject
{
    [ObservableProperty]
    private string? caption = null;

    [ObservableProperty]
    private BitmapImage? background = null;

    [ObservableProperty]
    private DoomEntry? currentEntry = null;

    [ObservableProperty]
    private string? progressText = null;

    [ObservableProperty]
    private bool isLeftDropHelperVisible;

    [ObservableProperty]
    private bool isRightDropHelperVisible;
}
