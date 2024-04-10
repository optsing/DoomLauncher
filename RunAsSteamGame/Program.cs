using Steamworks;
using System;
using System.Diagnostics;
using System.IO;

try
{
    SteamClient.Init(uint.Parse(args[0]), true);
    ProcessStartInfo startInfo = new ProcessStartInfo()
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
    SteamClient.Shutdown();
}
