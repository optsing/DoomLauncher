using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DoomLauncher;
/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class EditFileDialog : ContentDialog
{
    public EditFileDialog(XamlRoot root, object properties)
    {
        InitializeComponent();
        XamlRoot = root;
        FileDescription = "";
    }

    public string FileDescription
    {
        get; private set;
    }
}
