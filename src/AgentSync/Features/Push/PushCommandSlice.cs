using System.CommandLine;
using AgentSync.Application.Workflows;
using AgentSync.Composition;
using AgentSync.Presentation;

namespace AgentSync.Features.Push;

internal sealed class PushCommandSlice(PushWorkflowService workflowService, IConsoleRenderer renderer) : ICommandSlice
{
    public Command Create()
    {
        var command = new Command("push", "Push local managed changes back to the configured source.");
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
        var messageOption = new Option<string?>("--message", "-m")
        {
            Description = "Optional commit message used for GitHub-backed sources.",
            Required = false
        };

        command.Arguments.Add(nameArgument);
        command.Options.Add(catalogOption);
        command.Options.Add(localOption);
        command.Options.Add(platformOption);
        command.Options.Add(scopeOption);
        command.Options.Add(typeOption);
        command.Options.Add(dryRunOption);
        command.Options.Add(messageOption);
        command.SetAction(parseResult => CommandExecution.Execute(
            () => workflowService.Push(
                parseResult.GetValue(nameArgument)!,
                parseResult.GetValue(catalogOption),
                parseResult.GetValue(localOption),
                parseResult.GetValue(platformOption),
                parseResult.GetValue(scopeOption),
                parseResult.GetValue(typeOption),
                parseResult.GetValue(dryRunOption),
                parseResult.GetValue(messageOption)),
            renderer));

        return command;
    }
}
