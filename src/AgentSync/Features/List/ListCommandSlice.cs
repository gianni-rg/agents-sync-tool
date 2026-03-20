using System.CommandLine;
using AgentSync.Application.Context;
using AgentSync.Domain;
using AgentSync.Application.State;
using AgentSync.Composition;
using AgentSync.Presentation;

namespace AgentSync.Features.List;

internal sealed class ListCommandSlice(
    CatalogContextFactory catalogContextFactory,
    InstallStateRepository installStateRepository,
    IConsoleRenderer renderer) : ICommandSlice
{
    public Command Create()
    {
        var command = new Command("list", "List catalog entries and their install status.");
        var catalogOption = CommandOptions.CreateCatalogOption();
        var localOption = CommandOptions.CreateLocalOption();
        var platformOption = CommandOptions.CreatePlatformOption();
        var scopeOption = CommandOptions.CreateScopeOption();

        command.Options.Add(catalogOption);
        command.Options.Add(localOption);
        command.Options.Add(platformOption);
        command.Options.Add(scopeOption);
        command.SetAction(parseResult => CommandExecution.Execute(
            () => Handle(
                parseResult.GetValue(catalogOption),
                parseResult.GetValue(localOption),
                parseResult.GetValue(platformOption),
                parseResult.GetValue(scopeOption)),
            renderer));

        return command;
    }

    private int Handle(string? catalogInput, DirectoryInfo? localPath, string? platform, string? scope)
    {
        var context = catalogContextFactory.Create(catalogInput, localPath, platform, scope);
        var state = installStateRepository.Load(context.StateFilePath);
        var installed = state.Items
            .Select(x => $"{x.Type}:{x.Name}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rows = context.Catalog.EnumerateEntries()
            .Select(item => new CatalogListRow(
                item.Type,
                item.Entry.Name,
                item.Entry.Description,
                installed.Contains($"{item.Type.ToTypeString()}:{item.Entry.Name}"),
                item.Entry.Source,
                item.Entry.Tags is { Count: > 0 } ? string.Join(", ", item.Entry.Tags) : string.Empty))
            .ToList();

        renderer.ShowCatalog(context.CatalogFilePath, context.Platform, context.Scope, rows);
        return 0;
    }
}
