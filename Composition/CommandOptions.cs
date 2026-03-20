using System.CommandLine;

namespace AgentSync.Composition;

internal static class CommandOptions
{
    public static Option<string?> CreateCatalogOption() =>
        new("--catalog", "-c")
        {
            Description = "Path to catalog.json or the catalog repository root. Defaults to ./catalog.json if present.",
            Required = false
        };

    public static Option<DirectoryInfo?> CreateLocalOption() =>
        new("--local", "-l")
        {
            Description = "Local repo path. Defaults to the current directory for repo-scoped operations.",
            Required = false
        };

    public static Option<string?> CreatePlatformOption() =>
        new("--platform")
        {
            Description = "Target platform from catalog.json. Default: copilot.",
            Required = false
        };

    public static Option<string?> CreateScopeOption() =>
        new("--scope")
        {
            Description = "Target scope from catalog.json. Default: repo.",
            Required = false
        };

    public static Option<string?> CreateTypeOption() =>
        new("--type", "-t")
        {
            Description = "Asset type: agent, prompt, skill, or instruction.",
            Required = false
        };

    public static Option<bool> CreateDryRunOption() =>
        new("--dry-run", "-d")
        {
            Description = "Show what would happen without making changes."
        };

    public static Option<bool> CreateForceOption() =>
        new("--force", "-f")
        {
            Description = "Allow AgentSync to overwrite existing unmanaged content."
        };
}
