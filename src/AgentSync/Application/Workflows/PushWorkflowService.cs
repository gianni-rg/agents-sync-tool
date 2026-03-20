using AgentSync.Application.Catalog;
using AgentSync.Application.Context;
using AgentSync.Domain;
using AgentSync.Infrastructure.FileSystem;
using AgentSync.Infrastructure.GitHub;
using AgentSync.Infrastructure.Sources;
using AgentSync.Presentation;

namespace AgentSync.Application.Workflows;

internal sealed class PushWorkflowService(
    CatalogContextFactory catalogContextFactory,
    CatalogLookupService catalogLookupService,
    PathService pathService,
    LocalSourceService localSourceService,
    FileSystemService fileSystemService,
    GitHubService gitHubService,
    IConsoleRenderer renderer)
{
    public int Push(
        string name,
        string? catalogInput,
        DirectoryInfo? localPath,
        string? platform,
        string? scope,
        string? type,
        bool dryRun,
        string? message)
    {
        var context = catalogContextFactory.Create(catalogInput, localPath, platform, scope);
        var entryRef = catalogLookupService.ResolveEntryReference(context.Catalog, name, type);
        var localInstalledPath = pathService.ResolveInstalledTargetPath(context, entryRef);

        if (!File.Exists(localInstalledPath) && !Directory.Exists(localInstalledPath))
        {
            throw new InvalidOperationException(
                $"Managed content was not found at '{localInstalledPath}'. Install or sync the asset before pushing.");
        }

        if (gitHubService.TryParseGitHubSource(entryRef.Entry.Source, out var gitHubSource))
        {
            gitHubService.Push(entryRef, gitHubSource, localInstalledPath, dryRun, message, (action, target) => renderer.ShowWorkflowAction(action, target, dryRun));
        }
        else
        {
            var sourcePath = localSourceService.ResolveLocalSourcePath(context.CatalogDirectory, entryRef.Entry.Source);
            if (entryRef.Type == AssetType.Skill)
            {
                fileSystemService.SyncDirectory(localInstalledPath, sourcePath, dryRun, (action, target) => renderer.ShowWorkflowAction(action, target, dryRun));
            }
            else
            {
                fileSystemService.CopyFileIfNeeded(localInstalledPath, sourcePath, dryRun, overwriteExisting: true, (action, target) => renderer.ShowWorkflowAction(action, target, dryRun));
            }
        }

        renderer.ShowSummary($"Pushed {entryRef.Type.ToTypeString()}:{entryRef.Entry.Name}.");
        return 0;
    }
}
