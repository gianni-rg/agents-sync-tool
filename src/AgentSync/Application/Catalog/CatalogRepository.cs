using System.Text.Json;
using AgentSync.Domain;
using AgentSync.Domain.Catalog;

namespace AgentSync.Application.Catalog;

internal sealed class CatalogRepository(
    JsonSerializerOptions jsonOptions,
    PathService pathService,
    CatalogLookupService catalogLookupService)
{
    public string ResolveCatalogFilePath(string? catalogInput)
    {
        if (!string.IsNullOrWhiteSpace(catalogInput))
        {
            var expanded = pathService.ExpandConfiguredPath(catalogInput.Trim());
            if (Directory.Exists(expanded))
            {
                var filePath = Path.Combine(expanded, "catalog.json");
                if (File.Exists(filePath))
                {
                    return filePath;
                }

                throw new InvalidOperationException($"catalog.json not found in directory: {expanded}");
            }

            if (File.Exists(expanded))
            {
                return expanded;
            }

            throw new InvalidOperationException($"Catalog path does not exist: {expanded}");
        }

        var defaultCandidate = Path.Combine(Directory.GetCurrentDirectory(), "catalog.json");
        if (File.Exists(defaultCandidate))
        {
            return defaultCandidate;
        }

        throw new InvalidOperationException("Unable to find catalog.json. Use --catalog to specify a catalog path.");
    }

    public CatalogDocument Load(string catalogFilePath)
    {
        var catalogJson = File.ReadAllText(catalogFilePath);
        var catalog = JsonSerializer.Deserialize<CatalogDocument>(catalogJson, jsonOptions)
                      ?? throw new InvalidOperationException("Unable to deserialize catalog.json.");
        Validate(catalog, catalogFilePath);
        return catalog;
    }

    public void Save(string catalogFilePath, CatalogDocument catalog)
    {
        File.WriteAllText(catalogFilePath, JsonSerializer.Serialize(catalog, jsonOptions));
    }

    public void Validate(CatalogDocument catalog, string catalogFilePath)
    {
        if (catalog.Version <= 0)
        {
            throw new InvalidOperationException($"catalog.json has an invalid version in {catalogFilePath}.");
        }

        if (catalog.Targets.Count == 0)
        {
            throw new InvalidOperationException($"catalog.json in {catalogFilePath} must define at least one platform in targets.");
        }

        foreach (var platform in catalog.Targets)
        {
            if (platform.Value.Count == 0)
            {
                throw new InvalidOperationException($"Platform '{platform.Key}' in {catalogFilePath} must define at least one scope.");
            }
        }

        var seenEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entryRef in catalog.EnumerateEntries())
        {
            if (string.IsNullOrWhiteSpace(entryRef.Entry.Name))
            {
                throw new InvalidOperationException($"{entryRef.Type.ToTypeString()} entries in {catalogFilePath} must have a non-empty name.");
            }

            if (string.IsNullOrWhiteSpace(entryRef.Entry.Source))
            {
                throw new InvalidOperationException($"{entryRef.Type.ToTypeString()} '{entryRef.Entry.Name}' must have a non-empty source.");
            }

            var typedKey = $"{entryRef.Type.ToTypeString()}:{entryRef.Entry.Name}";
            if (!seenEntries.Add(typedKey))
            {
                throw new InvalidOperationException($"catalog.json contains a duplicate entry: {typedKey}");
            }

            foreach (var dependency in entryRef.Entry.Requires ?? [])
            {
                catalogLookupService.ValidateTypedReference(dependency);
            }
        }
    }

    public string NormalizeCatalogSourceForStorage(string catalogDirectory, string source)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri)
            && (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
                || uri.Host.Equals("raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase)))
        {
            return source.Trim();
        }

        var expanded = pathService.ExpandConfiguredPath(source.Trim());
        var resolved = Path.IsPathRooted(expanded) ? expanded : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), expanded));
        if (!File.Exists(resolved) && !Directory.Exists(resolved))
        {
            throw new InvalidOperationException($"Source path does not exist: {resolved}");
        }

        return Path.GetRelativePath(catalogDirectory, resolved);
    }
}
