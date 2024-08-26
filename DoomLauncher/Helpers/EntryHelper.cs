using DoomLauncher.ViewModels;
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
    public static async Task<DoomEntryViewModel?> ImportFromGZDLFile(StorageFile file, bool withConfirm, Action<string?> SetProgress)
    {
        try
        {
            SetProgress(Strings.Resources.ProgressReadFile(file.Name));
            using var zipToRead = await file.OpenStreamForReadAsync();
            using var archive = new ZipArchive(zipToRead, ZipArchiveMode.Read);
            DoomEntryViewModel? newEntry = null;
            if (archive.Entries.FirstOrDefault(entry => entry.FullName == "entry.json") is ZipArchiveEntry zipConfigEntry)
            {
                SetProgress(Strings.Resources.ProgressExtract(zipConfigEntry.Name));
                using var configStream = zipConfigEntry.Open();
                newEntry = await JsonSerializer.DeserializeAsync(configStream, JsonSettingsContext.Default.DoomEntryViewModel);
            }
            newEntry ??= new DoomEntryViewModel()
            {
                Name = Path.GetFileNameWithoutExtension(file.Name).Trim(),
            };
            var modFiles = new List<string>();
            var imageFiles = new List<string>();
            if (withConfirm)
            {
                foreach (var zipFileEntry in archive.Entries)
                {
                    var ext = Path.GetExtension(zipFileEntry.Name).ToLowerInvariant();
                    if (FileHelper.SupportedModExtensions.Contains(ext))
                    {
                        modFiles.Add(zipFileEntry.Name);
                    }
                    else if (FileHelper.SupportedImageExtensions.Contains(ext))
                    {
                        imageFiles.Add(zipFileEntry.Name);
                    }
                }
                if (await DialogHelper.ShowEditEntryAsync(newEntry, EditDialogMode.Import, modFiles, imageFiles) is EditEntryDialogViewModel result)
                {
                    result.UpdateEntry(newEntry);
                    modFiles = result.GetModFiles();
                    imageFiles = result.GetImageFiles();
                }
                else
                {
                    newEntry = null;
                }
            }
            if (newEntry != null)
            {
                var modsCopied = new List<string>();
                var imagesCopied = new List<string>();
                foreach (var zipFileEntry in archive.Entries)
                {
                    var ext = Path.GetExtension(zipFileEntry.Name).ToLowerInvariant();
                    if (FileHelper.SupportedModExtensions.Contains(ext) && (!withConfirm || modFiles.Contains(zipFileEntry.Name)))
                    {
                        SetProgress(Strings.Resources.ProgressExtract(zipFileEntry.Name));
                        using var fileStream = zipFileEntry.Open();
                        await FileHelper.CopyFileWithConfirmation(fileStream, zipFileEntry.Name, FileHelper.ModsFolderPath);
                        modsCopied.Add(zipFileEntry.Name);
                    }
                    else if (FileHelper.SupportedImageExtensions.Contains(ext) && (!withConfirm || imageFiles.Contains(zipFileEntry.Name)))
                    {
                        SetProgress(Strings.Resources.ProgressExtract(zipFileEntry.Name));
                        using var fileStream = zipFileEntry.Open();
                        await FileHelper.CopyFileWithConfirmation(fileStream, zipFileEntry.Name, FileHelper.ImagesFolderPath);
                        imagesCopied.Add(zipFileEntry.Name);
                    }
                }

                newEntry.Id = Guid.NewGuid().ToString();
                newEntry.Created = DateTime.Now;
                newEntry.ModFiles = new(newEntry.ModFiles
                    .Where(modsCopied.Contains)
                    .Concat(modsCopied
                        .Where(fileName => !newEntry.ModFiles.Contains(fileName))
                     ));
                newEntry.ImageFiles = new(newEntry.ImageFiles
                    .Where(imagesCopied.Contains)
                    .Concat(imagesCopied
                        .Where(fileName => !newEntry.ImageFiles.Contains(fileName))
                    ));

                SetProgress(null);
                return newEntry;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }
        SetProgress(null);
        return null;
    }

    public static async Task<DoomEntryViewModel?> ImportFromDoomWorld(DoomWorldFileEntry wadInfo, bool withConfirm, Action<string?> SetProgress)
    {
        try
        {
            SetProgress(Strings.Resources.ProgressReadFile(wadInfo.Filename));
            using var zipToRead = await WebAPI.Current.DownloadDoomWorldWadArchive(wadInfo);
            using var archive = new ZipArchive(zipToRead, ZipArchiveMode.Read);

            var newEntry = new DoomEntryViewModel()
            {
                Name = wadInfo.Title.Trim(),
                LongDescription = reLineBreak().Replace(wadInfo.Description, "\n").Trim(),
            };
            var modFiles = new List<string>();
            var imageFiles = new List<string>();
            if (withConfirm)
            {
                foreach (var zipFileEntry in archive.Entries)
                {
                    var ext = Path.GetExtension(zipFileEntry.Name).ToLowerInvariant();
                    if (FileHelper.SupportedModExtensions.Contains(ext))
                    {
                        modFiles.Add(zipFileEntry.Name);
                    }
                    else if (FileHelper.SupportedImageExtensions.Contains(ext))
                    {
                        imageFiles.Add(zipFileEntry.Name);
                    }
                }
                if (await DialogHelper.ShowEditEntryAsync(newEntry, EditDialogMode.Import, modFiles, imageFiles) is EditEntryDialogViewModel result)
                {
                    result.UpdateEntry(newEntry);
                    modFiles = result.GetModFiles();
                    imageFiles = result.GetImageFiles();
                }
                else
                {
                    newEntry = null;
                }
            }
            if (newEntry != null)
            {
                var modsCopied = new List<string>();
                var imagesCopied = new List<string>();
                foreach (var zipFileEntry in archive.Entries)
                {
                    var ext = Path.GetExtension(zipFileEntry.Name).ToLowerInvariant();
                    if (FileHelper.SupportedModExtensions.Contains(ext) && (!withConfirm || modFiles.Contains(zipFileEntry.Name)))
                    {
                        SetProgress(Strings.Resources.ProgressExtract(zipFileEntry.Name));
                        using var fileStream = zipFileEntry.Open();
                        await FileHelper.CopyFileWithConfirmation(fileStream, zipFileEntry.Name, FileHelper.ModsFolderPath);
                        modsCopied.Add(zipFileEntry.Name);
                    }
                    else if (FileHelper.SupportedImageExtensions.Contains(ext) && (!withConfirm || imageFiles.Contains(zipFileEntry.Name)))
                    {
                        SetProgress(Strings.Resources.ProgressExtract(zipFileEntry.Name));
                        using var fileStream = zipFileEntry.Open();
                        await FileHelper.CopyFileWithConfirmation(fileStream, zipFileEntry.Name, FileHelper.ImagesFolderPath);
                        imagesCopied.Add(zipFileEntry.Name);
                    }
                }
                newEntry.Id = Guid.NewGuid().ToString();
                newEntry.Created = DateTime.Now;
                newEntry.SelectedImageIndex = 0;
                newEntry.ModFiles = new(modsCopied);
                newEntry.ImageFiles = new(imagesCopied);
                SetProgress(null);
                return newEntry;
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

    public static async Task<DoomEntryViewModel?> CreateFromFiles(IReadOnlyList<StorageFile> files, bool withConfirm, Action<string?> SetProgress)
    {
        try
        {
            var mods = files.Where(file => FileHelper.SupportedModExtensions.Contains(Path.GetExtension(file.Name))).ToList();
            var images = files.Where(file => FileHelper.SupportedImageExtensions.Contains(Path.GetExtension(file.Name))).ToList();
            string title = Strings.Resources.NewEntry;
            if (mods.Count > 0)
            {
                title = Path.GetFileNameWithoutExtension(mods.First().Name);
            }
            else if (images.Count > 0)
            {
                title = Path.GetFileNameWithoutExtension(images.First().Name);
            }

            var newEntry = new DoomEntryViewModel()
            {
                Name = title.Trim(),
            };
            var modFiles = new List<string>();
            var imageFiles = new List<string>();
            if (withConfirm)
            {
                foreach (var mod in mods)
                {
                    modFiles.Add(mod.Name);
                }
                foreach (var image in images)
                {
                    imageFiles.Add(image.Name);
                }
                if (await DialogHelper.ShowEditEntryAsync(newEntry, EditDialogMode.Create, modFiles, imageFiles) is EditEntryDialogViewModel result)
                {
                    result.UpdateEntry(newEntry);
                    modFiles = result.GetModFiles();
                    imageFiles = result.GetImageFiles();
                }
                else
                {
                    newEntry = null;
                }
            }
            if (newEntry != null)
            {
                var modsCopied = new List<string>();
                var imagesCopied = new List<string>();
                foreach (var mod in mods)
                {
                    if (!withConfirm || modFiles.Contains(mod.Name))
                    {
                        SetProgress(Strings.Resources.ProgressCopy(mod.Name));
                        await FileHelper.CopyFileWithConfirmation(mod, FileHelper.ModsFolderPath);
                        modsCopied.Add(mod.Name);
                    }
                }
                foreach (var image in images)
                {
                    if (!withConfirm || imageFiles.Contains(image.Name))
                    {
                        SetProgress(Strings.Resources.ProgressCopy(image.Name));
                        await FileHelper.CopyFileWithConfirmation(image, FileHelper.ImagesFolderPath);
                        imagesCopied.Add(image.Name);
                    }
                }
                newEntry.Id = Guid.NewGuid().ToString();
                newEntry.Created = DateTime.Now;
                newEntry.SelectedImageIndex = 0;
                newEntry.ModFiles = new(modsCopied);
                newEntry.ImageFiles = new(imagesCopied);
                SetProgress(null);
                return newEntry;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }
        SetProgress(null);
        return null;
    }

    public static async Task ExportToGZDLFile(DoomEntryViewModel entry, StorageFile file, Action<string?> SetProgress)
    {
        SetProgress(Strings.Resources.ProgressExport(file.Name));
        using var zipToWrite = await file.OpenStreamForWriteAsync();
        using var archive = new ZipArchive(zipToWrite, ZipArchiveMode.Create);

        var zipConfigEntry = archive.CreateEntry("entry.json");
        using (var configStream = zipConfigEntry.Open())
        {
            var fileName = "entry.json";
            SetProgress(Strings.Resources.ProgressExport(fileName));
            var newEntry = new DoomEntryViewModel()
            {
                Id = entry.Id,
                Created = entry.Created,
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
            await JsonSerializer.SerializeAsync(configStream, newEntry, JsonSettingsContext.Default.DoomEntryViewModel);
        }

        foreach (var filePath in entry.ModFiles)
        {
            var fullPath = Path.GetFullPath(filePath, FileHelper.ModsFolderPath);
            var fileName = Path.GetFileName(filePath);
            SetProgress(Strings.Resources.ProgressExport(fileName));
            var zipFileEntry = archive.CreateEntry(Path.Combine("mods", fileName));
            using var fileStream = zipFileEntry.Open();
            await File.OpenRead(fullPath).CopyToAsync(fileStream);
        }
        foreach (var filePath in entry.ImageFiles)
        {
            var fullPath = Path.GetFullPath(filePath, FileHelper.ImagesFolderPath);
            var fileName = Path.GetFileName(filePath);
            SetProgress(Strings.Resources.ProgressExport(fileName));
            var zipFileEntry = archive.CreateEntry(Path.Combine("images", fileName));
            using var fileStream = zipFileEntry.Open();
            await File.OpenRead(fullPath).CopyToAsync(fileStream);
        }
        SetProgress(null);
    }
}
