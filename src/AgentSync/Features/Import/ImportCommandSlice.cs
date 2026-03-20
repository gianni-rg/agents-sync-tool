using System.CommandLine;
using AgentSync.Application.Workflows;
using AgentSync.Composition;
using AgentSync.Presentation;

namespace AgentSync.Features.Import;

internal sealed class ImportCommandSlice(ImportWorkflowService workflowService, IConsoleRenderer renderer) : ICommandSlice
{
    public Command Create()
    {
        var command = new Command("import", "Discover unmanaged assets and bring matching catalog entries under management.");

        var catalogOption = CommandOptions.CreateCatalogOption();
        var localOption = CommandOptions.CreateLocalOption();
        var platformOption = CommandOptions.CreatePlatformOption();
        var scopeOption = CommandOptions.CreateScopeOption();
        var dryRunOption = CommandOptions.CreateDryRunOption();
        var forceOption = CommandOptions.CreateForceOption();
        var addUnmappedOption = new Option<bool>("--add-unmapped")
        {
            Description = "Automatically add unmapped discovered assets to catalog.json before importing them."
        };
        var sourceOption = new Option<string?>("--source", "-s")
        {
            Description = "Known source alias or path to scan. Supported aliases: auto, vscode, copilot, repo.",
            Required = false
        };

        command.Options.Add(catalogOption);
        command.Options.Add(localOption);
        command.Options.Add(platformOption);
        command.Options.Add(scopeOption);
        command.Options.Add(sourceOption);
        command.Options.Add(dryRunOption);
        command.Options.Add(forceOption);
        command.Options.Add(addUnmappedOption);
        command.SetAction(parseResult => CommandExecution.Execute(
            () => workflowService.Import(
                parseResult.GetValue(catalogOption),
                parseResult.GetValue(localOption),
                parseResult.GetValue(platformOption),
                parseResult.GetValue(scopeOption),
                parseResult.GetValue(sourceOption),
                parseResult.GetValue(dryRunOption),
                parseResult.GetValue(forceOption),
                parseResult.GetValue(addUnmappedOption)),
            renderer));

        return command;
    }
}
