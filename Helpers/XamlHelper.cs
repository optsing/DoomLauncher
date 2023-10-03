using Microsoft.UI.Xaml;
using System;
using System.Collections.ObjectModel;

namespace DoomLauncher;

static class XamlHelper
{
    public static Visibility HasText(string text)
    {
        return string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    public static bool HasMoreItems(int itemsCount, int count)
    {
        return itemsCount > count;
    }

    public static Visibility HasItems(int itemsCount)
    {
        return itemsCount > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility HasNoItems(int itemsCount)
    {
        return itemsCount == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public static string DateToText(DateTime? lastLaunch)
    {
        return lastLaunch?.ToString() ?? "Никогда";
    }

    public static string FileInFavoritesGlyph(Collection<string> list, string value)
    {
        return list.Contains(value) ? "\uE734" : "\uE735";
    }

    public static string FileInFavoritesTooltip(Collection<string> list, string value)
    {
        return list.Contains(value) ? "Убрать из избранного" : "Добавить в избранное";
    }

    public static string IsDefaultText(object value1, object value2)
    {
        return Equals(value1, value2) ? " по умолчанию" : "";
    }
}
