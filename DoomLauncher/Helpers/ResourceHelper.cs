using Microsoft.Windows.ApplicationModel.Resources;

namespace DoomLauncher.Helpers;

public static class ResourceExtensions
{
    private static readonly ResourceMap Strings = new ResourceManager().MainResourceMap;

    public static string GetLocalized(this string value)
    {
        var localized = Strings.TryGetValue(value);
        return localized != null ? localized.ValueAsString : value;
    }

    public static string GetLocalized(this string value, params string[] strings)
    {
        var localized = Strings.TryGetValue(value);
        return localized != null ? localized.ValueAsString : string.Format(value, strings);
    }
}
