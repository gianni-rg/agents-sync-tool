using System.CommandLine;
using AgentSync.Application.Catalog;
using AgentSync.Domain;
using AgentSync.Composition;
using AgentSync.Domain.Catalog;
using AgentSync.Presentation;

namespace AgentSync.Features.Add;

internal sealed class AddCommandSlice(
    CatalogRepository catalogRepository,
    CatalogLookupService catalogLookupService,
    IConsoleRenderer renderer) : ICommandSlice
{
    public Command Create()
    {
        var command = new Command("add", "Register a new asset in catalog.json.");
        var nameArgument = new Argument<string>("name")
        {
            Description = "Catalog entry name."
        };
        var catalogOption = CommandOptions.CreateCatalogOption();
        var typeOption = new Option<string>("--type", "-t")
        {
            Description = "Asset type: agent, prompt, skill, or instruction."
        };
        var sourceOption = new Option<string>("--source", "-s")
        {
            Description = "Local filesystem path or supported GitHub URL for the source."
        };
        var descriptionOption = new Option<string?>("--description")
        {
            Description = "Optional short description stored in catalog.json."
        };
        var tagsOption = new Option<string[]>("--tag")
        {
            Description = "Optional tag. Repeat --tag for multiple values."
        };
        var requiresOption = new Option<string[]>("--require")
        {
            Description = "Typed dependency reference such as skill:common. Repeat --require for multiple values."
        };

        command.Arguments.Add(nameArgument);
        command.Options.Add(catalogOption);
        command.Options.Add(typeOption);
        command.Options.Add(sourceOption);
        command.Options.Add(descriptionOption);
        command.Options.Add(tagsOption);
        command.Options.Add(requiresOption);
        command.SetAction(parseResult => CommandExecution.Execute(
            () => Handle(
                parseResult.GetValue(nameArgument)!,
                parseResult.GetValue(catalogOption),
                parseResult.GetValue(typeOption),
                parseResult.GetValue(sourceOption),
                parseResult.GetValue(descriptionOption),
                parseResult.GetValue(tagsOption) ?? [],
                parseResult.GetValue(requiresOption) ?? []),
            renderer));

        return command;
    }

    private int Handle(
        string name,
        string? catalogInput,
        string? type,
        string? source,
        string? description,
        IReadOnlyCollection<string> tags,
        IReadOnlyCollection<string> requires)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new InvalidOperationException("The --type option is required for add.");
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new InvalidOperationException("The --source option is required for add.");
        }

        var catalogFilePath = catalogRepository.ResolveCatalogFilePath(catalogInput);
        var catalogDirectory = Path.GetDirectoryName(catalogFilePath)
                               ?? throw new InvalidOperationException("Unable to resolve catalog directory.");
        var catalog = catalogRepository.Load(catalogFilePath);
        var assetType = catalogLookupService.ParseAssetType(type);

        if (catalog.EnumerateEntries().Any(x =>
                x.Type == assetType && x.Entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Catalog entry already exists: {assetType.ToTypeString()}:{name}");
        }

        var normalizedRequires = requires
            .SelectMany(catalogLookupService.SplitOptionValues)
            .ToList();
        foreach (var dependency in normalizedRequires)
        {
            catalogLookupService.ValidateTypedReference(dependency);
        }

        var entry = new CatalogEntry
        {
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty,
            Source = catalogRepository.NormalizeCatalogSourceForStorage(catalogDirectory, source),
            Requires = normalizedRequires.Count == 0 ? null : normalizedRequires,
            Tags = tags.SelectMany(catalogLookupService.SplitOptionValues).ToList() is { Count: > 0 } normalizedTags ? normalizedTags : null
        };

        catalog.AddEntry(assetType, entry);
        catalogRepository.Validate(catalog, catalogFilePath);
        catalogRepository.Save(catalogFilePath, catalog);

        renderer.ShowSummary($"Added {assetType.ToTypeString()}:{entry.Name} to {catalogFilePath}.");
        return 0;
    }
}
