using Microsoft.UI.Xaml;
using System;
using System.Collections.ObjectModel;
using Humanizer;

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
        return lastLaunch?.Humanize() ?? "Неизвестно";
    }

    public static string TimeSpanToText(TimeSpan? time)
    {
        return time?.Humanize() ?? "Неизвестно";
    }

    public static string IsDefaultText(object value1, object value2)
    {
        return Equals(value1, value2) ? " по умолчанию" : "";
    }

    public static string EntryTooltip(string firstLine, string secondLine)
    {
        if (string.IsNullOrWhiteSpace(secondLine))
        {
            return firstLine;
        }
        return $"{firstLine}\n{secondLine}";
    }
}
