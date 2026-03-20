using AgentSync.Domain;
using AgentSync.Domain.Catalog;

namespace AgentSync.Application.Catalog;

internal sealed class CatalogLookupService
{
    public AssetType ParseAssetType(string type)
    {
        return type.Trim().ToLowerInvariant() switch
        {
            "agent" or "agents" => AssetType.Agent,
            "prompt" or "prompts" => AssetType.Prompt,
            "skill" or "skills" => AssetType.Skill,
            "instruction" or "instructions" => AssetType.Instruction,
            _ => throw new InvalidOperationException($"Unsupported asset type: {type}")
        };
    }

    public void ValidateTypedReference(string dependency)
    {
        var parts = dependency.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            throw new InvalidOperationException($"Invalid typed dependency reference: {dependency}");
        }

        _ = ParseAssetType(parts[0]);
    }

    public CatalogEntryReference ResolveEntryReference(CatalogDocument catalog, string name, string? type)
    {
        AssetType? requestedType = null;
        if (!string.IsNullOrWhiteSpace(type))
        {
            requestedType = ParseAssetType(type);
        }

        var matches = catalog.EnumerateEntries()
            .Where(x => x.Entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                        && (!requestedType.HasValue || x.Type == requestedType.Value))
            .ToList();

        if (matches.Count == 0)
        {
            throw new InvalidOperationException($"Catalog entry not found: {name}");
        }

        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Multiple catalog entries matched '{name}'. Re-run with --type. Matches: {string.Join(", ", matches.Select(x => x.Type.ToTypeString()))}");
        }

        return matches[0];
    }

    public CatalogEntryReference ResolveTypedReference(CatalogDocument catalog, string typedReference)
    {
        var parts = typedReference.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            throw new InvalidOperationException($"Invalid dependency reference: {typedReference}");
        }

        return ResolveEntryReference(catalog, parts[1], parts[0]);
    }

    public List<CatalogEntryReference> FilterEntries(IEnumerable<CatalogEntryReference> entries, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return entries.ToList();
        }

        var parts = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return entries
            .Where(entry =>
                parts.All(part =>
                    entry.Entry.Name.Contains(part, StringComparison.OrdinalIgnoreCase)
                    || entry.Entry.Description.Contains(part, StringComparison.OrdinalIgnoreCase)
                    || (entry.Entry.Tags?.Any(tag => tag.Contains(part, StringComparison.OrdinalIgnoreCase)) ?? false)))
            .ToList();
    }

    public IEnumerable<string> SplitOptionValues(string value)
    {
        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x));
    }
}
