using AgentSync.Domain.Catalog;
using AgentSync.Domain.Sources;
using AgentSync.Infrastructure.GitHub;

namespace AgentSync.Infrastructure.Sources;

internal sealed class AssetSourceResolver(
    GitHubService gitHubService,
    LocalSourceService localSourceService)
{
    public ResolvedAssetSource ResolveEntrySource(CatalogContext context, CatalogEntryReference entryRef)
    {
        if (gitHubService.TryParseGitHubSource(entryRef.Entry.Source, out var gitHubSource))
        {
            return gitHubService.DownloadSource(entryRef, gitHubSource);
        }

        var sourcePath = localSourceService.ResolveLocalSourcePath(context.CatalogDirectory, entryRef.Entry.Source);
        return new ResolvedAssetSource(sourcePath, false);
    }
}
