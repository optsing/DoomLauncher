using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;

namespace DoomLauncher.Helpers;

internal static class BitmapHelper
{
    private static readonly Dictionary<string, Task<BitmapImage?>> BitmapCache = [];

    private static readonly Dictionary<string, Task<BitmapImage?>> PreviewCache = [];

    public static Task<BitmapImage?> CreateBitmapFromFile(string filePath, bool isPreview)
    {
        var cache = isPreview ? PreviewCache : BitmapCache;
        if (cache.TryGetValue(filePath, out Task<BitmapImage?>? value))
        {
            return value;
        }
        var task = LoadImage(filePath, isPreview);
        cache[filePath] = task;
        return task;
    }

    private static async Task<BitmapImage?> LoadImage(string filePath, bool isPreview)
    {
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(filePath);
            var bitmapImage = new BitmapImage();
            if (isPreview)
            {
                bitmapImage.DecodePixelType = DecodePixelType.Logical;
                bitmapImage.DecodePixelWidth = 192;
            }
            await bitmapImage.SetSourceAsync(await file.OpenReadAsync());
            return bitmapImage;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }
        return null;
    }
}
