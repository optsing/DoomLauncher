using DoomLauncher.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DoomLauncher;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class EditEntryDialog : ContentDialog
{
    public EditEntryDialogViewModel ViewModel { get; set; }

    public EditEntryDialog(EditEntryDialogViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
    }

    private void EditEntryDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.Name))
        {
            tbModName.Focus(FocusState.Programmatic);
            args.Cancel = true;
        }
    }
}

public enum EditDialogMode
{
    Create, Edit, Import, Copy
}

public class TitleChecked(string title)
{
    public string Title { get; set; } = title;
    public bool IsChecked { get; set; } = true;
}

public readonly struct KeyValue(string key, string value)
{
    public readonly string Key = key;
    public readonly string Value = value;
}
