using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;

namespace DoomLauncher;

internal static class BitmapHelper
{
    private static readonly Dictionary<string, BitmapImage> BitmapCache = new();
    public static async Task<BitmapImage?> CreateBitmapFromFile(string filePath)
    {
        if (BitmapCache.ContainsKey(filePath))
        {
            return BitmapCache[filePath];
        }
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(filePath);
            var bitmapImage = new BitmapImage();
            await bitmapImage.SetSourceAsync(await file.OpenReadAsync());
            BitmapCache[filePath] = bitmapImage;
            return bitmapImage;
        }
        catch
        {
            return null;
        }
    }
}
