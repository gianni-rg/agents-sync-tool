using System.CommandLine;
using AgentSync.Application.Workflows;
using AgentSync.Composition;
using AgentSync.Presentation;

namespace AgentSync.Features.Use;

internal sealed class UseCommandSlice(InstallWorkflowService workflowService, IConsoleRenderer renderer) : ICommandSlice
{
    public Command Create()
    {
        var command = new Command("use", "Install one catalog asset and its dependencies.");
        var nameArgument = new Argument<string>("name")
        {
            Description = "Catalog entry name."
        };
        var catalogOption = CommandOptions.CreateCatalogOption();
        var localOption = CommandOptions.CreateLocalOption();
        var platformOption = CommandOptions.CreatePlatformOption();
        var scopeOption = CommandOptions.CreateScopeOption();
        var typeOption = CommandOptions.CreateTypeOption();
        var dryRunOption = CommandOptions.CreateDryRunOption();
        var forceOption = CommandOptions.CreateForceOption();

        command.Arguments.Add(nameArgument);
        command.Options.Add(catalogOption);
        command.Options.Add(localOption);
        command.Options.Add(platformOption);
        command.Options.Add(scopeOption);
        command.Options.Add(typeOption);
        command.Options.Add(dryRunOption);
        command.Options.Add(forceOption);
        command.SetAction(parseResult => CommandExecution.Execute(
            () => workflowService.Use(
                parseResult.GetValue(nameArgument)!,
                parseResult.GetValue(catalogOption),
                parseResult.GetValue(localOption),
                parseResult.GetValue(platformOption),
                parseResult.GetValue(scopeOption),
                parseResult.GetValue(typeOption),
                parseResult.GetValue(dryRunOption),
                parseResult.GetValue(forceOption)),
            renderer));

        return command;
    }
}
