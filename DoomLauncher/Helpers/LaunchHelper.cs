using System.Diagnostics;
using System.IO;

namespace DoomLauncher;

public enum LaunchResult
{
    Success, AlreadyLaunched, NotLaunched, PathNotValid
}

internal static class LaunchHelper
{
    public static Process? CurrentProcess { get; private set; }
    public static LaunchResult LaunchEntry(DoomEntry entry)
    {
        if (CurrentProcess != null && !CurrentProcess.HasExited)
        {
            return LaunchResult.AlreadyLaunched;
        }
        var package = FileHelper.ResolveGZDoomPath(entry.GZDoomPath, Settings.Current.DefaultGZDoomPath);
        if (package == null)
        {
            return LaunchResult.PathNotValid;
        }
        var gZDoomPath = Path.GetFullPath(package.Path, FileHelper.PackagesFolderPath);
        if (!FileHelper.ValidateGZDoomPath(gZDoomPath))
        {
            return LaunchResult.PathNotValid;
        }
        ProcessStartInfo processInfo;
        var steamAppId = FileHelper.GetSteamAppIdForEntry(entry);
        if (steamAppId == 0)
        {
            processInfo = new()
            {
                FileName = gZDoomPath,
                WorkingDirectory = Path.GetDirectoryName(gZDoomPath),
            };
        }
        else
        {
            processInfo = new()
            {
                FileName = "RunAsSteamGame.exe",
            };
            processInfo.ArgumentList.Add(steamAppId.ToString());
            processInfo.ArgumentList.Add(gZDoomPath);
        }
        if (entry.UniqueConfig)
        {
            var entryFolderPath = Path.Combine(FileHelper.EntriesFolderPath, entry.Id);
            if (!Directory.Exists(entryFolderPath))
            {
                Directory.CreateDirectory(entryFolderPath);
            }
            var configPath = Path.Combine(entryFolderPath, "config.ini");
            processInfo.ArgumentList.Add("-config");
            processInfo.ArgumentList.Add(configPath);
        }
        if (entry.UniqueSavesFolder)
        {
            var entrySavesFolderPath = Path.Combine(FileHelper.EntriesFolderPath, entry.Id, "saves");
            if (!Directory.Exists(entrySavesFolderPath))
            {
                Directory.CreateDirectory(entrySavesFolderPath);
            }
            processInfo.ArgumentList.Add("-savedir");
            processInfo.ArgumentList.Add(entrySavesFolderPath);
        }
        var resolvedIWadFile = FileHelper.ResolveIWadFile(entry.IWadFile, Settings.Current.DefaultIWadFile);
        if (!string.IsNullOrEmpty(resolvedIWadFile))
        {
            processInfo.ArgumentList.Add("-iwad");
            processInfo.ArgumentList.Add(Path.GetFullPath(resolvedIWadFile, FileHelper.IWadFolderPath));
        }
        if (entry.ModFiles.Count > 0)
        {
            processInfo.ArgumentList.Add("-file");
            foreach (var filePath in entry.ModFiles)
            {
                processInfo.ArgumentList.Add(Path.GetFullPath(filePath, FileHelper.ModsFolderPath));
            }
        }
        processInfo.RedirectStandardError = true;
        CurrentProcess = Process.Start(processInfo);
        if (CurrentProcess == null)
        {
            return LaunchResult.NotLaunched;
        }
        return LaunchResult.Success;
    }
}
