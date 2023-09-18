using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DoomLauncher;

public class LaunchOptions
{
    [Option("launch", Required = false, HelpText = "Entry id to launch")]
    public string? EntryId { get; set; }

    [Option("force-close", Required = false, HelpText = "Force close on launch")]
    public bool CloseOnLaunch { get; set; }
}

internal static class CommandLine
{

    public static LaunchOptions? ParseCommandLine(string commandline)
    {
        var args = WinApi.CommandLineToArgs(commandline);
        if (Parser.Default.ParseArguments<LaunchOptions>(args) is Parsed<LaunchOptions> result)
        {
            return result.Value;
        }
        return null;
    }
}

