using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DoomLauncher;
/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class EditModContentDialog : Page
{
    public EditModContentDialog(EditModDialogResult initial)
    {
        InitializeComponent();
        ModName = initial.name;
        IWadFile = initial.iWadFile;
        CloseOnLaunch = initial.closeOnLaunch;
    }

    public string ModName
    {
        get; private set;
    }

    public KeyValue IWadFile
    {
        get; private set;
    }

    public bool CloseOnLaunch
    {
        get; set;
    }
}

public readonly struct EditModDialogResult
{
    public readonly string name;
    public readonly KeyValue iWadFile;
    public readonly bool closeOnLaunch;

    public EditModDialogResult(string name, KeyValue iWadFile, bool closeOnLaunch)
    {
        this.name = name;
        this.iWadFile = iWadFile;
        this.closeOnLaunch = closeOnLaunch;
    }
}