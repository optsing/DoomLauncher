using DoomLauncher.ViewModels;
using System.Diagnostics;
using System.IO;

namespace DoomLauncher.Helpers;

public enum LaunchResult
{
    Success, AlreadyLaunched, NotLaunched, PathNotValid
}

internal static class LaunchHelper
{
    public static Process? CurrentProcess { get; private set; }
    public static LaunchResult LaunchEntry(DoomEntryViewModel entry)
    {
        if (CurrentProcess != null && !CurrentProcess.HasExited)
        {
            return LaunchResult.AlreadyLaunched;
        }
        var package = FileHelper.ResolveGZDoomPath(entry.GZDoomPath, SettingsViewModel.Current.DefaultGZDoomPath);
        if (package == null)
        {
            return LaunchResult.PathNotValid;
        }
        var gZDoomPath = Path.GetFullPath(package.Path, FileHelper.PackagesFolderPath);
        if (!FileHelper.ValidateGZDoomPath(gZDoomPath))
        {
            return LaunchResult.PathNotValid;
        }
        var processInfo = new ProcessStartInfo();
        var steamAppId = FileHelper.ResolveSteamGame(entry.SteamGame, SettingsViewModel.Current.SteamGame, entry.IWadFile, SettingsViewModel.Current.DefaultIWadFile).appId;
        if (steamAppId == 0)
        {
            processInfo.FileName = gZDoomPath;
            processInfo.WorkingDirectory = Path.GetDirectoryName(gZDoomPath);
        }
        else
        {
            processInfo.FileName = "RunAsSteamGame.exe";
            processInfo.UseShellExecute = false;
            processInfo.CreateNoWindow = true;
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
        var resolvedIWadFile = FileHelper.ResolveIWadFile(entry.IWadFile, SettingsViewModel.Current.DefaultIWadFile);
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
