using System.Text.Json;
using AgentSync.Domain.Catalog;

namespace AgentSync.Tests.Support;

internal static class TestFiles
{
    public static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "AgentSync.Tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(path);
        return path;
    }

    public static void DeleteTempDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    public static string WriteCatalog(string catalogRoot, string sourcePath, string? name = null, string? type = null)
    {
        Directory.CreateDirectory(catalogRoot);

        var entryName = name ?? Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(sourcePath));
        var assetType = type ?? "agent";
        var relativeSource = Path.GetRelativePath(catalogRoot, sourcePath);

        var catalog = new CatalogDocument
        {
            Targets = new Dictionary<string, Dictionary<string, TargetDirectories>>(StringComparer.OrdinalIgnoreCase)
            {
                ["copilot"] = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["repo"] = new TargetDirectories
                    {
                        Agents = ".github/agents",
                        Prompts = ".github/prompts",
                        Skills = ".github/skills",
                        Instructions = ".github/instructions"
                    }
                }
            }
        };

        var entry = new CatalogEntry
        {
            Name = entryName,
            Source = relativeSource
        };

        switch (assetType)
        {
            case "agent":
                catalog.Catalog.Agents.Add(entry);
                break;
            case "prompt":
                catalog.Catalog.Prompts.Add(entry);
                break;
            case "skill":
                catalog.Catalog.Skills.Add(entry);
                break;
            case "instruction":
                catalog.Catalog.Instructions.Add(entry);
                break;
            default:
                throw new InvalidOperationException($"Unsupported test asset type: {assetType}");
        }

        var catalogPath = Path.Combine(catalogRoot, "catalog.json");
        File.WriteAllText(catalogPath, JsonSerializer.Serialize(catalog, new JsonSerializerOptions { WriteIndented = true }));
        return catalogPath;
    }
}
