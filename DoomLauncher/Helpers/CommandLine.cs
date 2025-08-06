using System.CommandLine;

namespace DoomLauncher.Helpers;

public class LaunchOptions
{
    public string? EntryId { get; set; }
    public string? EntryName { get; set; }
    public bool ForceClose { get; set; }
}

internal static class CommandLine
{
    public static LaunchOptions? ParseCommandLine(string commandLine)
    {
        var idOption = new Option<string>("--id") { Description = "Entry id to launch" };
        var nameOption = new Option<string>("--name") { Description = "Entry name to launch" };
        var forceCloseOption = new Option<bool>("--force-close") { Description = "Force close after launch" };
        var launchCommand = new Command("launch", "Launch entry on app start")
        {
            idOption,
            nameOption,
            forceCloseOption,
        };
        var rootCommand = new RootCommand
        {
            launchCommand,
        };

        rootCommand.TreatUnmatchedTokensAsErrors = false;

        var parseResult = rootCommand.Parse(commandLine);
        if (parseResult.GetResult(launchCommand) is { } launchResult)
        {
            return new()
            {
                EntryId = launchResult.GetValue(idOption),
                EntryName = launchResult.GetValue(nameOption),
                ForceClose = launchResult.GetValue(forceCloseOption),
            };
        }

        return null;
    }
}
