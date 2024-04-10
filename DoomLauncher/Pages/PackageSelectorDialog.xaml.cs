using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Linq;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DoomLauncher;
/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class PackageSelectorDialog : ContentDialog
{
    public PackageSelectorDialog(List<DoomPackageViewModel> assets)
    {
        InitializeComponent();
        OnlinePackage = assets;
        SelectedPackage = assets.FirstOrDefault();
    }

    private List<DoomPackageViewModel> OnlinePackage { get; }

    public DoomPackageViewModel? SelectedPackage { get; set; }
}

