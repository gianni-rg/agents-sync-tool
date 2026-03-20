using System.CommandLine;
using AgentSync.Application.Catalog;
using AgentSync.Application.Context;
using AgentSync.Composition;
using AgentSync.Presentation;

namespace AgentSync.Features.Search;

internal sealed class SearchCommandSlice(
    CatalogContextFactory catalogContextFactory,
    CatalogLookupService catalogLookupService,
    IConsoleRenderer renderer) : ICommandSlice
{
    public Command Create()
    {
        var command = new Command("search", "Search catalog entries by name, description, and tags.");
        var queryArgument = new Argument<string?>("query")
        {
            Description = "Search text. If omitted, all catalog entries are shown.",
            Arity = ArgumentArity.ZeroOrOne
        };

        var catalogOption = CommandOptions.CreateCatalogOption();
        var localOption = CommandOptions.CreateLocalOption();
        var platformOption = CommandOptions.CreatePlatformOption();
        var scopeOption = CommandOptions.CreateScopeOption();

        command.Arguments.Add(queryArgument);
        command.Options.Add(catalogOption);
        command.Options.Add(localOption);
        command.Options.Add(platformOption);
        command.Options.Add(scopeOption);
        command.SetAction(parseResult => CommandExecution.Execute(
            () => Handle(
                parseResult.GetValue(queryArgument),
                parseResult.GetValue(catalogOption),
                parseResult.GetValue(localOption),
                parseResult.GetValue(platformOption),
                parseResult.GetValue(scopeOption)),
            renderer));

        return command;
    }

    private int Handle(string? query, string? catalogInput, DirectoryInfo? localPath, string? platform, string? scope)
    {
        var context = catalogContextFactory.Create(catalogInput, localPath, platform, scope);
        var filtered = catalogLookupService.FilterEntries(context.Catalog.EnumerateEntries(), query);
        var rows = filtered
            .Select(item => new SearchResultRow(
                item.Type,
                item.Entry.Name,
                item.Entry.Description,
                item.Entry.Tags is { Count: > 0 } ? string.Join(", ", item.Entry.Tags) : string.Empty))
            .ToList();

        renderer.ShowSearch(
            context.CatalogFilePath,
            string.IsNullOrWhiteSpace(query) ? "(all entries)" : query.Trim(),
            rows);
        return 0;
    }
}
