using System.Runtime.InteropServices;
using AgentSync.Domain;
using AgentSync.Domain.Catalog;

namespace AgentSync.Application.Catalog;

internal sealed class PathService
{
    public string ExpandConfiguredPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        if (path.StartsWith("~/.copilot", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("~\\.copilot", StringComparison.OrdinalIgnoreCase))
        {
            var copilotHome = GetCopilotHomePath();
            var remainder = path["~/.copilot".Length..].TrimStart('/', '\\');
            if (string.IsNullOrWhiteSpace(remainder))
            {
                return copilotHome;
            }

            remainder = remainder.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            return Path.Combine(copilotHome, remainder);
        }

        if (path.Equals("~", StringComparison.Ordinal))
        {
            return GetUserHomePath();
        }

        if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
        {
            var home = GetUserHomePath();
            var remainder = path[2..].Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            return Path.Combine(home, remainder);
        }

        return Environment.ExpandEnvironmentVariables(path);
    }

    public string GetUserHomePath() =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public string GetCopilotHomePath()
    {
        var configured = Environment.GetEnvironmentVariable("COPILOT_HOME");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return ExpandConfiguredPath(configured);
        }

        return Path.Combine(GetUserHomePath(), ".copilot");
    }

    public string GetVsCodeUserPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Code", "User");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Path.Combine(GetUserHomePath(), "Library", "Application Support", "Code", "User");
        }

        return Path.Combine(GetUserHomePath(), ".config", "Code", "User");
    }

    public string ResolveInstalledTargetPath(CatalogContext context, CatalogEntryReference entryRef)
    {
        var targetRoot = ResolveTargetRoot(context, entryRef.Type);
        return entryRef.Type switch
        {
            AssetType.Skill => Path.Combine(targetRoot, entryRef.Entry.Name),
            AssetType.Agent or AssetType.Prompt or AssetType.Instruction => Path.Combine(targetRoot, ResolveAssetFileName(entryRef)),
            _ => throw new InvalidOperationException($"Unsupported asset type: {entryRef.Type}")
        };
    }

    public string ResolveTargetRoot(CatalogContext context, AssetType type)
    {
        var relativeOrAbsolutePath = type switch
        {
            AssetType.Agent => context.TargetDirectories.Agents,
            AssetType.Prompt => context.TargetDirectories.Prompts,
            AssetType.Skill => context.TargetDirectories.Skills,
            AssetType.Instruction => context.TargetDirectories.Instructions,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath))
        {
            throw new InvalidOperationException(
                $"No target path configured for {type.ToTypeString()} in platform '{context.Platform}' scope '{context.Scope}'.");
        }

        var expanded = ExpandConfiguredPath(relativeOrAbsolutePath);
        if (Path.IsPathRooted(expanded))
        {
            return expanded;
        }

        if (context.Scope.Equals("repo", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFullPath(Path.Combine(context.LocalRoot, expanded));
        }

        return Path.GetFullPath(Path.Combine(GetUserHomePath(), expanded));
    }

    public string ResolveAssetFileName(CatalogEntryReference entryRef)
    {
        if (Uri.TryCreate(entryRef.Entry.Source, UriKind.Absolute, out var uri))
        {
            var fileName = Path.GetFileName(Uri.UnescapeDataString(uri.AbsolutePath));
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return fileName;
            }
        }

        var expanded = ExpandConfiguredPath(entryRef.Entry.Source);
        var fileNameFromSource = Path.GetFileName(expanded);
        if (!string.IsNullOrWhiteSpace(fileNameFromSource))
        {
            return fileNameFromSource;
        }

        return entryRef.Type switch
        {
            AssetType.Agent => $"{entryRef.Entry.Name}.agent.md",
            AssetType.Prompt => $"{entryRef.Entry.Name}.prompt.md",
            AssetType.Instruction => $"{entryRef.Entry.Name}.instructions.md",
            _ => throw new InvalidOperationException($"Unsupported file-based asset type: {entryRef.Type}")
        };
    }
}
