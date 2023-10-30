using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DoomLauncher;

internal static class DialogHelper
{
    public static XamlRoot? XamlRoot { get; set; }

    public static async Task<bool> ShowAskAsync(string title, string text, string primaryButton, string closeButton)
    {
        if (XamlRoot == null)
        {
            return false;
        }
        try
        {
            var dialog = new AskDialog(title, text, primaryButton, closeButton)
            {
                XamlRoot = XamlRoot,
            };
            return ContentDialogResult.Primary == await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return false;
        }
    }

    public static async Task<EditEntryDialogViewModel?> ShowEditEntryAsync(DoomEntry entry, EditDialogMode mode, List<string>? modFiles = null, List<string>? imageFiles = null)
    {
        if (XamlRoot == null)
        {
            return null;
        }
        var viewModel = EditEntryDialogViewModel.FromEntry(entry, mode, modFiles, imageFiles);
        var dialog = new EditEntryDialog(viewModel)
        {
            XamlRoot = XamlRoot,
        };
        if (ContentDialogResult.Primary == await dialog.ShowAsync())
        {
            return viewModel;
        }
        return null;
    }

    public static async Task<EditEntryDialogViewModel?> ShowEditEntryAsync(EditDialogMode mode)
    {
        if (XamlRoot == null)
        {
            return null;
        }
        var viewModel = new EditEntryDialogViewModel(mode);
        var dialog = new EditEntryDialog(viewModel)
        {
            XamlRoot = XamlRoot,
        };
        if (ContentDialogResult.Primary == await dialog.ShowAsync())
        {
            return viewModel;
        }
        return null;
    }

    public static async Task<GZDoomPackage?> ShowPackageSelectorAsync(List<GZDoomPackage> packages)
    {
        if (XamlRoot == null)
        {
            return null;
        }
        var dialog = new PackageSelectorDialog(packages)
        {
            XamlRoot = XamlRoot,
        };
        if (ContentDialogResult.Primary == await dialog.ShowAsync())
        {
            return dialog.SelectedPackage;
        }
        return null;
    }
}
