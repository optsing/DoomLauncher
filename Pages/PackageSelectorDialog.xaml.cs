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
    public PackageSelectorDialog(XamlRoot root, List<GZDoomFileAsset> assets)
    {
        InitializeComponent();
        XamlRoot = root;
        Assets = assets;
        SelectedAsset = assets.FirstOrDefault();
    }

    public List<GZDoomFileAsset> Assets { get; }

    public GZDoomFileAsset? SelectedAsset { get; set; }

    public static async Task<GZDoomFileAsset?> ShowAsync(XamlRoot root, List<GZDoomFileAsset> assets)
    {
        var dialog = new PackageSelectorDialog(root, assets);
        if (ContentDialogResult.Primary == await dialog.ShowAsync())
        {
            return dialog.SelectedAsset;
        }
        return null;
    }
}
