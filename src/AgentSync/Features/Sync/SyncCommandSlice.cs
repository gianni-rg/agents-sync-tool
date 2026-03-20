using System.CommandLine;
using AgentSync.Application.Workflows;
using AgentSync.Composition;
using AgentSync.Presentation;

namespace AgentSync.Features.Sync;

internal sealed class SyncCommandSlice(InstallWorkflowService workflowService, IConsoleRenderer renderer) : ICommandSlice
{
    public Command Create()
    {
        var command = new Command("sync", "Refresh every asset previously installed by AgentSync for the selected platform and scope.");
        var catalogOption = CommandOptions.CreateCatalogOption();
        var localOption = CommandOptions.CreateLocalOption();
        var platformOption = CommandOptions.CreatePlatformOption();
        var scopeOption = CommandOptions.CreateScopeOption();
        var dryRunOption = CommandOptions.CreateDryRunOption();
        var forceOption = CommandOptions.CreateForceOption();

        command.Options.Add(catalogOption);
        command.Options.Add(localOption);
        command.Options.Add(platformOption);
        command.Options.Add(scopeOption);
        command.Options.Add(dryRunOption);
        command.Options.Add(forceOption);
        command.SetAction(parseResult => CommandExecution.Execute(
            () => workflowService.Sync(
                parseResult.GetValue(catalogOption),
                parseResult.GetValue(localOption),
                parseResult.GetValue(platformOption),
                parseResult.GetValue(scopeOption),
                parseResult.GetValue(dryRunOption),
                parseResult.GetValue(forceOption)),
            renderer));

        return command;
    }
}
