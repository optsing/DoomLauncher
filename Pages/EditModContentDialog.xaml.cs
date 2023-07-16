using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DoomLauncher;
/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class EditModContentDialog : ContentDialog
{
    public EditModContentDialog(XamlRoot root, EditModDialogResult initial, List<KeyValue> filteredIWads, bool isEditMode)
    {
        InitializeComponent();
        XamlRoot = root;
        ModName = initial.name;
        IWadFile = initial.iWadFile;
        FilteredIWads = filteredIWads;
        PrimaryButtonText = isEditMode ? "Сохранить" : "Создать";
        this.Title = isEditMode ? "Настройка сборки" : "Создание сборки";
    }

    private void EditModContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (ModName == "")
        {
            tbModName.Focus(FocusState.Programmatic);
            args.Cancel = true;
        }
    }

    public List<KeyValue> FilteredIWads {
        get;
        private set;
    }

    public string ModName
    {
        get; private set;
    }

    public KeyValue IWadFile
    {
        get; private set;
    }
}

public readonly struct EditModDialogResult
{
    public readonly string name;
    public readonly KeyValue iWadFile;

    public EditModDialogResult(string name, KeyValue iWadFile)
    {
        this.name = name;
        this.iWadFile = iWadFile;
    }
}