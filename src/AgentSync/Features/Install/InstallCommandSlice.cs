using System.CommandLine;
using AgentSync.Application.Workflows;
using AgentSync.Composition;
using AgentSync.Presentation;

namespace AgentSync.Features.Install;

internal sealed class InstallCommandSlice(InstallWorkflowService workflowService, IConsoleRenderer renderer) : ICommandSlice
{
    public Command Create()
    {
        var command = new Command("install", "Install all catalog assets or the named subset into the selected target.");
        var namesArgument = new Argument<string[]>("names")
        {
            Description = "Optional catalog entry names to install. If omitted, every entry is installed.",
            Arity = ArgumentArity.ZeroOrMore
        };
        var catalogOption = CommandOptions.CreateCatalogOption();
        var localOption = CommandOptions.CreateLocalOption();
        var platformOption = CommandOptions.CreatePlatformOption();
        var scopeOption = CommandOptions.CreateScopeOption();
        var dryRunOption = CommandOptions.CreateDryRunOption();
        var forceOption = CommandOptions.CreateForceOption();

        command.Arguments.Add(namesArgument);
        command.Options.Add(catalogOption);
        command.Options.Add(localOption);
        command.Options.Add(platformOption);
        command.Options.Add(scopeOption);
        command.Options.Add(dryRunOption);
        command.Options.Add(forceOption);
        command.SetAction(parseResult => CommandExecution.Execute(
            () => workflowService.Install(
                parseResult.GetValue(namesArgument) ?? [],
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
