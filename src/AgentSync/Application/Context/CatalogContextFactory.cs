using AgentSync.Application.Catalog;
using AgentSync.Application.State;
using AgentSync.Domain.Catalog;

namespace AgentSync.Application.Context;

internal sealed class CatalogContextFactory(
    CatalogRepository catalogRepository,
    InstallStateRepository installStateRepository)
{
    public CatalogContext Create(string? catalogInput, DirectoryInfo? localPath, string? platform, string? scope)
    {
        var catalogFilePath = catalogRepository.ResolveCatalogFilePath(catalogInput);
        var catalogDirectory = Path.GetDirectoryName(catalogFilePath)
                               ?? throw new InvalidOperationException("Unable to resolve catalog directory.");
        var catalog = catalogRepository.Load(catalogFilePath);

        var effectivePlatform = string.IsNullOrWhiteSpace(platform) ? "copilot" : platform.Trim();
        var effectiveScope = string.IsNullOrWhiteSpace(scope) ? "repo" : scope.Trim();
        var targetDirectories = catalog.GetTargetDirectories(effectivePlatform, effectiveScope);

        var localRoot = localPath?.FullName ?? Directory.GetCurrentDirectory();
        if (effectiveScope.Equals("repo", StringComparison.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(localRoot);
        }

        var stateFilePath = installStateRepository.ResolveStateFilePath(effectiveScope, localRoot, effectivePlatform);

        return new CatalogContext(
            catalogFilePath,
            catalogDirectory,
            catalog,
            effectivePlatform,
            effectiveScope,
            localRoot,
            targetDirectories,
            stateFilePath);
    }
}
