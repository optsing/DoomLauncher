using Steamworks;
using System;
using System.Diagnostics;
using System.IO;
using WindowsShortcutFactory;

try
{
    string fileName;
    string? workingDirectory;
    if (args.Length == 0)
    {
        throw new Exception("No args passed");
    }
    else if (args.Length == 1)
    {
        if (Path.GetExtension(args[0]) == ".lnk")
        {
            using var shortcut = WindowsShortcut.Load(args[0]);
            if (shortcut?.Path is string path)
            {
                fileName = path;
                workingDirectory = shortcut?.WorkingDirectory;
            }
            else
            {
                throw new Exception("Can't parse shortcut");
            }
        }
        else
        {
            fileName = args[0];
            workingDirectory = Path.GetDirectoryName(fileName);
        }
    }
    else
    {
        Environment.SetEnvironmentVariable("SteamAppId", args[0]);
        Environment.SetEnvironmentVariable("SteamGameId", args[0]);
        fileName = args[1];
        workingDirectory = Path.GetDirectoryName(fileName);
    }
    if (!SteamAPI.Init())
    {
        throw new Exception("Can't initialize Steam API");
    }
    var startInfo = new ProcessStartInfo
    {
        FileName = fileName,
        WorkingDirectory = workingDirectory,
    };
    if (args.Length > 2)
    {
        foreach (string sub in args[2..])
        {
            startInfo.ArgumentList.Add(sub);
        }
    }
    var process = Process.Start(startInfo) ?? throw new Exception("No process was created");
    process.WaitForExit();
    Environment.ExitCode = process.ExitCode;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    Environment.ExitCode = -1;
}
finally
{
    SteamAPI.Shutdown();
}
