using Microsoft.UI.Xaml;
using System;

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

    public static string LastLaunchToText(DateTime? lastLaunch)
    {
        return $"Последний запуск: {lastLaunch?.ToString() ?? "Никогда"}";
    }
}
