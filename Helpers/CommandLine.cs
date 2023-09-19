using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DoomLauncher;

[Verb("launch", HelpText = "Launch entry on app start")]
public class LaunchOptions
{
    [Option("id", Group = "Entry", HelpText = "Entry id to launch")]
    public string? EntryId { get; set; }

    [Option("name", Group = "Entry", HelpText = "Entry name to launch")]
    public string? EntryName { get; set; }

    [Option("force-close", Required = false, HelpText = "Force close after launch")]
    public bool ForceClose { get; set; }
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

