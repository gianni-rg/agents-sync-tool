using System.CommandLine;

namespace AgentSync.Composition;

internal sealed class RootCommandFactory(IEnumerable<ICommandSlice> commandSlices)
{
    public RootCommand Create()
    {
        var rootCommand = new RootCommand("Manage catalog-driven Copilot assets.");

        foreach (var commandSlice in commandSlices)
        {
            rootCommand.Subcommands.Add(commandSlice.Create());
        }

        return rootCommand;
    }
}
