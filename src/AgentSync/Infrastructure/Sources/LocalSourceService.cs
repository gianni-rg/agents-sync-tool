using AgentSync.Application.Catalog;

namespace AgentSync.Infrastructure.Sources;

internal sealed class LocalSourceService(PathService pathService)
{
    public string ResolveLocalSourcePath(string catalogDirectory, string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new InvalidOperationException("Catalog entry source cannot be empty.");
        }

        var expanded = pathService.ExpandConfiguredPath(source);
        var resolved = Path.IsPathRooted(expanded) ? expanded : Path.GetFullPath(Path.Combine(catalogDirectory, expanded));

        if (File.Exists(resolved) || Directory.Exists(resolved))
        {
            return resolved;
        }

        throw new InvalidOperationException($"Source path not found: {resolved}");
    }

    public bool TryResolveEntryLocalSourcePath(string catalogDirectory, string source, out string resolvedPath)
    {
        resolvedPath = string.Empty;
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri)
            && (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
                || uri.Host.Equals("raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var expanded = pathService.ExpandConfiguredPath(source);
        resolvedPath = Path.IsPathRooted(expanded) ? expanded : Path.GetFullPath(Path.Combine(catalogDirectory, expanded));
        return File.Exists(resolvedPath) || Directory.Exists(resolvedPath);
    }

    public bool PathsEqual(string leftPath, string rightPath) =>
        Path.GetFullPath(leftPath).Equals(Path.GetFullPath(rightPath), StringComparison.OrdinalIgnoreCase);
}
