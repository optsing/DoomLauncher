using Steamworks;
using System;
using System.Diagnostics;
using System.IO;

try
{
    Environment.SetEnvironmentVariable("SteamAppId", args[0]);
    Environment.SetEnvironmentVariable("SteamGameId", args[0]);
    SteamAPI.Init();
    var startInfo = new ProcessStartInfo
    {
        FileName = args[1],
        WorkingDirectory = Path.GetDirectoryName(args[1])
    };
    foreach (string sub in args[2..])
    {
        startInfo.ArgumentList.Add(sub);
    }
    var process = Process.Start(startInfo);
    if (process == null)
    {
        Environment.Exit(-1);
    }
    process.WaitForExit();
    Environment.Exit(process.ExitCode);
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    Environment.Exit(-1);
}
finally
{
    SteamAPI.Shutdown();
}
