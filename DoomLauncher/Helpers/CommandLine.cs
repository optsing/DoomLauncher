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
        var idOption = new Option<string?>("--id", "Entry id to launch");
        var nameOption = new Option<string?>("--name", "Entry name to launch");
        var forceCloseOption = new Option<bool>("--force-close", "Force close after launch");
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

        LaunchOptions? result = null;

        launchCommand.SetHandler(
            (id, name, forceClose) => {
                result = new()
                {
                    EntryId = id,
                    EntryName = name,
                    ForceClose = forceClose,
                };
            },
            idOption, nameOption, forceCloseOption
        );

        rootCommand.Invoke(commandLine);

        return result;
    }
}
