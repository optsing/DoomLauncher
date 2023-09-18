using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DoomLauncher;
/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class PackageSelectorDialog : ContentDialog
{
    public PackageSelectorDialog(XamlRoot root, List<GZDoomPackage> assets)
    {
        InitializeComponent();
        XamlRoot = root;
        OnlinePackage = assets;
        SelectedPackage = assets.FirstOrDefault();
    }

    private List<GZDoomPackage> OnlinePackage { get; }

    private GZDoomPackage? SelectedPackage { get; set; }

    public static async Task<GZDoomPackage?> ShowAsync(XamlRoot root, List<GZDoomPackage> packages)
    {
        var dialog = new PackageSelectorDialog(root, packages);
        if (ContentDialogResult.Primary == await dialog.ShowAsync())
        {
            return dialog.SelectedPackage;
        }
        return null;
    }
}
