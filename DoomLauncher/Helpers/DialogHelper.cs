using DoomLauncher.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DoomLauncher.Helpers;

public class EditEntryDialogResult
{
    public required ContentDialogResult Result { get; init; }
    public required EditEntryDialogViewModel ViewModel { get; init; }
}


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

    public static async Task<EditEntryDialogResult> ShowEditEntryAsync(DoomEntryViewModel entry, EditDialogMode mode, List<string>? modFiles = null, List<string>? imageFiles = null)
    {
        var viewModel = EditEntryDialogViewModel.FromEntry(entry, mode, modFiles, imageFiles);
        var dialog = new EditEntryDialog(viewModel)
        {
            XamlRoot = XamlRoot,
        };
        return new EditEntryDialogResult {
            Result = await dialog.ShowAsync(),
            ViewModel = viewModel,
        };
    }

    public static async Task<EditEntryDialogResult> ShowEditEntryAsync(EditDialogMode mode)
    {
        var viewModel = new EditEntryDialogViewModel(mode);
        var dialog = new EditEntryDialog(viewModel)
        {
            XamlRoot = XamlRoot,
        };
        return new EditEntryDialogResult
        {
            Result = await dialog.ShowAsync(),
            ViewModel = viewModel,
        };
    }

    public static async Task<DoomPackageViewModel?> ShowPackageSelectorAsync(List<DoomPackageViewModel> packages)
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
