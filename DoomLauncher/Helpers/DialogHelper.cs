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
        var dialog = new AskDialog(title, text, primaryButton, "", closeButton)
        {
            XamlRoot = XamlRoot,
        };
        return ContentDialogResult.Primary == await dialog.ShowAsync();
    }

    public static async Task<ContentDialogResult> ShowAskAsync(string title, string text, string primaryButton, string secondaryButton, string closeButton)
    {
        if (XamlRoot == null)
        {
            return ContentDialogResult.None;
        }
        var dialog = new AskDialog(title, text, primaryButton, secondaryButton, closeButton)
        {
            XamlRoot = XamlRoot,
        };
        return await dialog.ShowAsync();
    }

    public static async Task ShowAskAsync(string title, string text, string closeButton)
    {
        if (XamlRoot == null)
        {
            return;
        }
        var dialog = new AskDialog(title, text, "", "", closeButton)
        {
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
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
}
