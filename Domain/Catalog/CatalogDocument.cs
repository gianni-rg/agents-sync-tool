using System.Text.Json.Serialization;
using AgentSync.Domain;

namespace AgentSync.Domain.Catalog;

internal sealed class CatalogDocument
{
    [JsonPropertyName("version")]
    public int Version { get; init; } = 1;

    [JsonPropertyName("targets")]
    public Dictionary<string, Dictionary<string, TargetDirectories>> Targets { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("catalog")]
    public CatalogCollections Catalog { get; init; } = new();

    public IEnumerable<CatalogEntryReference> EnumerateEntries()
    {
        foreach (var entry in Catalog.Agents)
        {
            yield return new CatalogEntryReference(AssetType.Agent, entry);
        }

        foreach (var entry in Catalog.Prompts)
        {
            yield return new CatalogEntryReference(AssetType.Prompt, entry);
        }

        foreach (var entry in Catalog.Skills)
        {
            yield return new CatalogEntryReference(AssetType.Skill, entry);
        }

        foreach (var entry in Catalog.Instructions)
        {
            yield return new CatalogEntryReference(AssetType.Instruction, entry);
        }
    }

    public List<CatalogEntry> GetEntries(AssetType type) =>
        type switch
        {
            AssetType.Agent => Catalog.Agents,
            AssetType.Prompt => Catalog.Prompts,
            AssetType.Skill => Catalog.Skills,
            AssetType.Instruction => Catalog.Instructions,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

    public void AddEntry(AssetType type, CatalogEntry entry)
    {
        GetEntries(type).Add(entry);
    }

    public TargetDirectories GetTargetDirectories(string platform, string scope)
    {
        if (!Targets.TryGetValue(platform, out var scopes))
        {
            throw new InvalidOperationException($"Platform '{platform}' was not found in catalog targets.");
        }

        if (!scopes.TryGetValue(scope, out var directories))
        {
            throw new InvalidOperationException($"Scope '{scope}' was not found for platform '{platform}' in catalog targets.");
        }

        return directories;
    }
}
