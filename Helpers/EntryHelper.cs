using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Storage;

namespace DoomLauncher;

internal static partial class EntryHelper
{
    public static async Task<DoomEntry?> ImportFromGZDLFile(XamlRoot xamlRoot, StorageFile file, bool withConfirm, Action<string?> SetProgress)
    {
        try
        {
            SetProgress($"Чтение файла: {file.Name}");
            using var zipToRead = await file.OpenStreamForReadAsync();
            using var archive = new ZipArchive(zipToRead, ZipArchiveMode.Read);
            DoomEntry? newEntry = null;
            if (archive.Entries.FirstOrDefault(entry => entry.FullName == "entry.json") is ZipArchiveEntry zipConfigEntry)
            {
                SetProgress($"Извлечение: {zipConfigEntry.Name}");
                using var configStream = zipConfigEntry.Open();
                newEntry = await JsonSerializer.DeserializeAsync(configStream, JsonSettingsContext.Default.DoomEntry);
            }
            newEntry ??= new DoomEntry()
            {
                Name = Path.GetFileNameWithoutExtension(file.Name),
            };
            var entryProperties = new EditEntryDialogResult(newEntry);
            if (withConfirm)
            {
                foreach (var zipFileEntry in archive.Entries)
                {
                    var ext = Path.GetExtension(zipFileEntry.Name).ToLowerInvariant();
                    if (FileHelper.SupportedModExtensions.Contains(ext))
                    {
                        entryProperties.modFiles.Add(zipFileEntry.Name);
                    }
                    else if (FileHelper.SupportedImageExtensions.Contains(ext))
                    {
                        entryProperties.imageFiles.Add(zipFileEntry.Name);
                    }
                }
                entryProperties = await EditEntryDialog.ShowAsync(xamlRoot, entryProperties, EditDialogMode.Import);
            }
            if (entryProperties != null)
            {
                var modsCopied = new List<string>();
                var imagesCopied = new List<string>();
                foreach (var zipFileEntry in archive.Entries)
                {
                    var ext = Path.GetExtension(zipFileEntry.Name).ToLowerInvariant();
                    if (FileHelper.SupportedModExtensions.Contains(ext) && (!withConfirm || entryProperties.modFiles.Contains(zipFileEntry.Name)))
                    {
                        SetProgress($"Извлечение: {zipFileEntry.Name}");
                        using var fileStream = zipFileEntry.Open();
                        await FileHelper.CopyFileWithConfirmation(xamlRoot, fileStream, zipFileEntry.Name, FileHelper.ModsFolderPath);
                        modsCopied.Add(zipFileEntry.Name);
                    }
                    else if (FileHelper.SupportedImageExtensions.Contains(ext) && (!withConfirm || entryProperties.imageFiles.Contains(zipFileEntry.Name)))
                    {
                        SetProgress($"Извлечение: {zipFileEntry.Name}");
                        using var fileStream = zipFileEntry.Open();
                        await FileHelper.CopyFileWithConfirmation(xamlRoot, fileStream, zipFileEntry.Name, FileHelper.ImagesFolderPath);
                        imagesCopied.Add(zipFileEntry.Name);
                    }
                }

                var finalModFiles = newEntry.ModFiles
                    .Where(modsCopied.Contains)
                    .Concat(modsCopied
                        .Where(fileName => !newEntry.ModFiles.Contains(fileName)));
                var finalImageFiles = newEntry.ImageFiles
                    .Where(imagesCopied.Contains)
                    .Concat(imagesCopied
                        .Where(fileName => !newEntry.ImageFiles.Contains(fileName)));

                SetProgress(null);
                return new DoomEntry()
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = entryProperties.name,
                    Description = entryProperties.description,
                    LongDescription = entryProperties.longDescription,
                    GZDoomPath = entryProperties.gZDoomPath,
                    IWadFile = entryProperties.iWadFile,
                    SteamGame = entryProperties.steamGame,
                    UniqueConfig = entryProperties.uniqueConfig,
                    UniqueSavesFolder = entryProperties.uniqueSavesFolder,
                    SelectedImageIndex = newEntry.SelectedImageIndex,
                    ModFiles = new(finalModFiles),
                    ImageFiles = new(finalImageFiles),
                };
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }
        SetProgress(null);
        return null;
    }

    public static async Task<DoomEntry?> ImportFromDoomWorld(XamlRoot xamlRoot, DoomWorldFileEntry wadInfo, bool withConfirm, Action<string?> SetProgress)
    {
        try
        {
            SetProgress($"Чтение файла: {wadInfo.Filename}");
            using var zipToRead = await WebAPI.Current.DownloadDoomWorldWadArchive(wadInfo);
            using var archive = new ZipArchive(zipToRead, ZipArchiveMode.Read);

            var entryProperties = new EditEntryDialogResult(new DoomEntry()
            {
                Name = wadInfo.Title,
                LongDescription = reLineBreak().Replace(wadInfo.Description, "\n"),
            });
            if (withConfirm)
            {
                foreach (var zipFileEntry in archive.Entries)
                {
                    var ext = Path.GetExtension(zipFileEntry.Name).ToLowerInvariant();
                    if (FileHelper.SupportedModExtensions.Contains(ext))
                    {
                        entryProperties.modFiles.Add(zipFileEntry.Name);
                    }
                    else if (FileHelper.SupportedImageExtensions.Contains(ext))
                    {
                        entryProperties.imageFiles.Add(zipFileEntry.Name);
                    }
                }
                entryProperties = await EditEntryDialog.ShowAsync(xamlRoot, entryProperties, EditDialogMode.Import);
            }
            if (entryProperties != null)
            {
                var modsCopied = new List<string>();
                var imagesCopied = new List<string>();
                foreach (var zipFileEntry in archive.Entries)
                {
                    var ext = Path.GetExtension(zipFileEntry.Name).ToLowerInvariant();
                    if (FileHelper.SupportedModExtensions.Contains(ext) && (!withConfirm || entryProperties.modFiles.Contains(zipFileEntry.Name)))
                    {
                        SetProgress($"Извлечение: {zipFileEntry.Name}");
                        using var fileStream = zipFileEntry.Open();
                        await FileHelper.CopyFileWithConfirmation(xamlRoot, fileStream, zipFileEntry.Name, FileHelper.ModsFolderPath);
                        modsCopied.Add(zipFileEntry.Name);
                    }
                    else if (FileHelper.SupportedImageExtensions.Contains(ext) && (!withConfirm || entryProperties.imageFiles.Contains(zipFileEntry.Name)))
                    {
                        SetProgress($"Извлечение: {zipFileEntry.Name}");
                        using var fileStream = zipFileEntry.Open();
                        await FileHelper.CopyFileWithConfirmation(xamlRoot, fileStream, zipFileEntry.Name, FileHelper.ImagesFolderPath);
                        imagesCopied.Add(zipFileEntry.Name);
                    }
                }

                SetProgress(null);
                return new DoomEntry()
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = entryProperties.name,
                    Description = entryProperties.description,
                    LongDescription = entryProperties.longDescription,
                    GZDoomPath = entryProperties.gZDoomPath,
                    IWadFile = entryProperties.iWadFile,
                    SteamGame = entryProperties.steamGame,
                    UniqueConfig = entryProperties.uniqueConfig,
                    UniqueSavesFolder = entryProperties.uniqueSavesFolder,
                    SelectedImageIndex = 0,
                    ModFiles = new(modsCopied),
                    ImageFiles = new(imagesCopied),
                };
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }
        SetProgress(null);
        return null;
    }

    [GeneratedRegex(@"\s*<br>\s*")]
    private static partial Regex reLineBreak();

    public static async Task<DoomEntry?> CreateEntryFromFiles(XamlRoot xamlRoot, IReadOnlyList<StorageFile> files, bool withConfirm, Action<string?> SetProgress)
    {
        try
        {
            var mods = files.Where(file => FileHelper.SupportedModExtensions.Contains(Path.GetExtension(file.Name))).ToList();
            var images = files.Where(file => FileHelper.SupportedImageExtensions.Contains(Path.GetExtension(file.Name))).ToList();
            string title = "Новая сборка";
            if (mods.Any())
            {
                title = Path.GetFileNameWithoutExtension(mods.First().Name);
            }
            else if (images.Any())
            {
                title = Path.GetFileNameWithoutExtension(images.First().Name);
            }

            var entryProperties = new EditEntryDialogResult(new DoomEntry()
            {
                Name = title,
            });
            if (withConfirm)
            {
                foreach (var mod in mods)
                {
                    entryProperties.modFiles.Add(mod.Name);
                }
                foreach (var image in images)
                {
                    entryProperties.imageFiles.Add(image.Name);
                }
                entryProperties = await EditEntryDialog.ShowAsync(xamlRoot, entryProperties, EditDialogMode.Create);
            }
            if (entryProperties != null)
            {
                var modsCopied = new List<string>();
                var imagesCopied = new List<string>();
                foreach (var mod in mods)
                {
                    if (!withConfirm || entryProperties.modFiles.Contains(mod.Name))
                    {
                        SetProgress($"Копирование: {mod.Name}");
                        await FileHelper.CopyFileWithConfirmation(xamlRoot, mod, FileHelper.ModsFolderPath);
                        modsCopied.Add(mod.Name);
                    }
                }
                foreach (var image in images)
                {
                    if (!withConfirm || entryProperties.imageFiles.Contains(image.Name))
                    {
                        SetProgress($"Копирование: {image.Name}");
                        await FileHelper.CopyFileWithConfirmation(xamlRoot, image, FileHelper.ImagesFolderPath);
                        imagesCopied.Add(image.Name);
                    }
                }

                SetProgress(null);
                return new DoomEntry()
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = entryProperties.name,
                    Description = entryProperties.description,
                    LongDescription = entryProperties.longDescription,
                    GZDoomPath = entryProperties.gZDoomPath,
                    IWadFile = entryProperties.iWadFile,
                    SteamGame = entryProperties.steamGame,
                    UniqueConfig = entryProperties.uniqueConfig,
                    UniqueSavesFolder = entryProperties.uniqueSavesFolder,
                    SelectedImageIndex = 0,
                    ModFiles = new(modsCopied),
                    ImageFiles = new(imagesCopied),
                };
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }
        SetProgress(null);
        return null;
    }

    public static async Task ExportToGZDLFile(DoomEntry entry, StorageFile file, Action<string?> SetProgress)
    {
        SetProgress($"Экспорт: {file.Name}");
        using var zipToWrite = await file.OpenStreamForWriteAsync();
        using var archive = new ZipArchive(zipToWrite, ZipArchiveMode.Create);

        var zipConfigEntry = archive.CreateEntry("entry.json");
        using (var configStream = zipConfigEntry.Open())
        {
            var fileName = "entry.json";
            SetProgress($"Экспорт: {fileName}");
            var newEntry = new DoomEntry()
            {
                Id = entry.Id,
                Name = entry.Name,
                Description = entry.Description,
                LongDescription = entry.LongDescription,
                GZDoomPath = entry.GZDoomPath,
                IWadFile = entry.IWadFile,
                SteamGame = entry.SteamGame,
                UniqueConfig = entry.UniqueConfig,
                UniqueSavesFolder = entry.UniqueSavesFolder,
                SelectedImageIndex = entry.SelectedImageIndex,
                ModFiles = new(entry.ModFiles.Select(path => Path.GetFileName(path))),
                ImageFiles = new(entry.ImageFiles.Select(path => Path.GetFileName(path))),
            };
            await JsonSerializer.SerializeAsync(configStream, newEntry, JsonSettingsContext.Default.DoomEntry);
        }

        foreach (var filePath in entry.ModFiles)
        {
            var fullPath = Path.GetFullPath(filePath, FileHelper.ModsFolderPath);
            var fileName = Path.GetFileName(filePath);
            SetProgress($"Экспорт: {fileName}");
            var zipFileEntry = archive.CreateEntry(Path.Combine("mods", fileName));
            using var fileStream = zipFileEntry.Open();
            await File.OpenRead(fullPath).CopyToAsync(fileStream);
        }
        foreach (var filePath in entry.ImageFiles)
        {
            var fullPath = Path.GetFullPath(filePath, FileHelper.ImagesFolderPath);
            var fileName = Path.GetFileName(filePath);
            SetProgress($"Экспорт: {fileName}");
            var zipFileEntry = archive.CreateEntry(Path.Combine("images", fileName));
            using var fileStream = zipFileEntry.Open();
            await File.OpenRead(fullPath).CopyToAsync(fileStream);
        }
        SetProgress(null);
    }
}
