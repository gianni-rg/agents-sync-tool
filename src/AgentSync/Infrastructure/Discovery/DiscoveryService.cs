using AgentSync.Application.Catalog;
using AgentSync.Domain;
using AgentSync.Domain.Catalog;
using AgentSync.Domain.Discovery;
using AgentSync.Infrastructure.Sources;

namespace AgentSync.Infrastructure.Discovery;

internal sealed class DiscoveryService(PathService pathService, LocalSourceService localSourceService)
{
    public List<DiscoveredAsset> DiscoverUnmanagedAssets(CatalogContext context, string? source)
    {
        var results = new Dictionary<string, DiscoveredAsset>(StringComparer.OrdinalIgnoreCase);
        foreach (var location in EnumerateDiscoveryLocations(context, source))
        {
            if (!Directory.Exists(location.Root))
            {
                continue;
            }

            foreach (var item in DiscoverAssetsInLocation(location))
            {
                results.TryAdd($"{item.Type.ToTypeString()}:{item.Path}", item);
            }
        }

        return results.Values.ToList();
    }

    public CatalogEntryReference? TryMapDiscoveredAsset(CatalogContext context, DiscoveredAsset item)
    {
        var matches = context.Catalog.EnumerateEntries()
            .Where(x => x.Type == item.Type)
            .ToList();

        foreach (var candidate in matches)
        {
            if (!string.IsNullOrWhiteSpace(candidate.Entry.Name)
                && candidate.Entry.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }

            if (localSourceService.TryResolveEntryLocalSourcePath(context.CatalogDirectory, candidate.Entry.Source, out var candidatePath)
                && localSourceService.PathsEqual(candidatePath, item.Path))
            {
                return candidate;
            }
        }

        return null;
    }

    private IEnumerable<DiscoveryLocation> EnumerateDiscoveryLocations(CatalogContext context, string? source)
    {
        if (string.IsNullOrWhiteSpace(source) || source.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var item in GetKnownDiscoveryLocations(context))
            {
                yield return item;
            }

            yield break;
        }

        if (source.Equals("vscode", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var item in GetVsCodeDiscoveryLocations())
            {
                yield return item;
            }

            yield break;
        }

        if (source.Equals("copilot", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var item in GetCopilotDiscoveryLocations())
            {
                yield return item;
            }

            yield break;
        }

        if (source.Equals("repo", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var item in GetRepoDiscoveryLocations(context.LocalRoot))
            {
                yield return item;
            }

            yield break;
        }

        var expandedPath = pathService.ExpandConfiguredPath(source);
        if (!Directory.Exists(expandedPath))
        {
            throw new InvalidOperationException($"Import source path not found: {expandedPath}");
        }

        yield return new DiscoveryLocation("custom-agents", AssetType.Agent, Path.Combine(expandedPath, "agents"));
        yield return new DiscoveryLocation("custom-prompts", AssetType.Prompt, Path.Combine(expandedPath, "prompts"));
        yield return new DiscoveryLocation("custom-skills", AssetType.Skill, Path.Combine(expandedPath, "skills"));
        yield return new DiscoveryLocation("custom-instructions", AssetType.Instruction, Path.Combine(expandedPath, "instructions"));
    }

    private IEnumerable<DiscoveryLocation> GetKnownDiscoveryLocations(CatalogContext context)
    {
        foreach (var item in GetVsCodeDiscoveryLocations())
        {
            yield return item;
        }

        foreach (var item in GetCopilotDiscoveryLocations())
        {
            yield return item;
        }

        foreach (var item in GetRepoDiscoveryLocations(context.LocalRoot))
        {
            yield return item;
        }
    }

    private IEnumerable<DiscoveryLocation> GetVsCodeDiscoveryLocations()
    {
        var root = pathService.GetVsCodeUserPath();
        yield return new DiscoveryLocation("vscode-agents", AssetType.Agent, Path.Combine(root, "agents"));
        yield return new DiscoveryLocation("vscode-prompts", AssetType.Prompt, Path.Combine(root, "prompts"));
        yield return new DiscoveryLocation("vscode-instructions", AssetType.Instruction, Path.Combine(root, "instructions"));
        yield return new DiscoveryLocation("vscode-skills", AssetType.Skill, Path.Combine(pathService.GetCopilotHomePath(), "skills"));
    }

    private IEnumerable<DiscoveryLocation> GetCopilotDiscoveryLocations()
    {
        var root = pathService.GetCopilotHomePath();
        yield return new DiscoveryLocation("copilot-agents", AssetType.Agent, Path.Combine(root, "agents"));
        yield return new DiscoveryLocation("copilot-prompts", AssetType.Prompt, Path.Combine(root, "prompts"));
        yield return new DiscoveryLocation("copilot-skills", AssetType.Skill, Path.Combine(root, "skills"));
        yield return new DiscoveryLocation("copilot-instructions", AssetType.Instruction, Path.Combine(root, "instructions"));
    }

    private IEnumerable<DiscoveryLocation> GetRepoDiscoveryLocations(string localRoot)
    {
        var githubRoot = Path.Combine(localRoot, ".github");
        yield return new DiscoveryLocation("repo-agents", AssetType.Agent, Path.Combine(githubRoot, "agents"));
        yield return new DiscoveryLocation("repo-prompts", AssetType.Prompt, Path.Combine(githubRoot, "prompts"));
        yield return new DiscoveryLocation("repo-skills", AssetType.Skill, Path.Combine(githubRoot, "skills"));
        yield return new DiscoveryLocation("repo-instructions", AssetType.Instruction, Path.Combine(githubRoot, "instructions"));
    }

    private static IEnumerable<DiscoveredAsset> DiscoverAssetsInLocation(DiscoveryLocation location)
    {
        if (location.Type == AssetType.Skill)
        {
            if (!Directory.Exists(location.Root))
            {
                yield break;
            }

            foreach (var directory in Directory.EnumerateDirectories(location.Root)
                         .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                yield return new DiscoveredAsset(location.Type, Path.GetFileName(directory), directory, location.Label);
            }

            yield break;
        }

        if (!Directory.Exists(location.Root))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(location.Root, GetDiscoveryPattern(location.Type), SearchOption.AllDirectories)
                     .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            yield return new DiscoveredAsset(location.Type, GetDiscoveredAssetName(location.Type, file), file, location.Label);
        }
    }

    private static string GetDiscoveryPattern(AssetType type) =>
        type switch
        {
            AssetType.Agent => "*.agent.md",
            AssetType.Prompt => "*.prompt.md",
            AssetType.Instruction => "*.instructions.md",
            _ => "*"
        };

    private static string GetDiscoveredAssetName(AssetType type, string path)
    {
        var fileName = Path.GetFileName(path);
        return type switch
        {
            AssetType.Agent when fileName.EndsWith(".agent.md", StringComparison.OrdinalIgnoreCase) =>
                fileName[..^".agent.md".Length],
            AssetType.Prompt when fileName.EndsWith(".prompt.md", StringComparison.OrdinalIgnoreCase) =>
                fileName[..^".prompt.md".Length],
            AssetType.Instruction when fileName.EndsWith(".instructions.md", StringComparison.OrdinalIgnoreCase) =>
                fileName[..^".instructions.md".Length],
            _ => Path.GetFileNameWithoutExtension(fileName)
        };
    }
}
