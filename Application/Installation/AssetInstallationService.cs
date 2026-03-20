using AgentSync.Application.Catalog;
using AgentSync.Application.State;
using AgentSync.Domain;
using AgentSync.Domain.Catalog;
using AgentSync.Domain.State;
using AgentSync.Domain.Workflows;
using AgentSync.Infrastructure.FileSystem;
using AgentSync.Infrastructure.Sources;
using AgentSync.Presentation;

namespace AgentSync.Application.Installation;

internal sealed class AssetInstallationService(
    CatalogLookupService catalogLookupService,
    AssetSourceResolver assetSourceResolver,
    PathService pathService,
    FileSystemService fileSystemService,
    InstallStateRepository installStateRepository,
    IConsoleRenderer renderer)
{
    public void InstallEntryRecursive(
        CatalogContext context,
        InstallState state,
        CatalogEntryReference entryRef,
        ExecutionOptions options,
        HashSet<string> installedThisRun)
    {
        var typedKey = $"{entryRef.Type.ToTypeString()}:{entryRef.Entry.Name}";
        if (!installedThisRun.Add(typedKey))
        {
            return;
        }

        foreach (var dependency in entryRef.Entry.Requires ?? [])
        {
            var dependencyRef = catalogLookupService.ResolveTypedReference(context.Catalog, dependency);
            InstallEntryRecursive(context, state, dependencyRef, options, installedThisRun);
        }

        InstallSingleEntry(context, state, entryRef, options);
    }

    public void PersistInstalledEntries(
        CatalogContext context,
        InstallState state,
        IEnumerable<string> installedEntries,
        bool dryRun)
    {
        if (dryRun)
        {
            return;
        }

        foreach (var typedReference in installedEntries)
        {
            var entryRef = catalogLookupService.ResolveTypedReference(context.Catalog, typedReference);
            state.Upsert(entryRef);
        }

        installStateRepository.Save(context.StateFilePath, state);
    }

    private void InstallSingleEntry(
        CatalogContext context,
        InstallState state,
        CatalogEntryReference entryRef,
        ExecutionOptions options)
    {
        var targetPath = pathService.ResolveInstalledTargetPath(context, entryRef);

        using var resolvedSource = assetSourceResolver.ResolveEntrySource(context, entryRef);
        if (entryRef.Type == AssetType.Skill)
        {
            if (!Directory.Exists(resolvedSource.LocalPath))
            {
                throw new InvalidOperationException($"Skill source was expected to resolve to a directory: {resolvedSource.LocalPath}");
            }

            fileSystemService.EnsureDirectoryInstallAllowed(targetPath, state, entryRef, options.Force);
            renderer.ShowWorkflowAction(Directory.Exists(targetPath) ? "sync" : "install", $"skill {entryRef.Entry.Name}", options.DryRun);
            fileSystemService.SyncDirectory(
                resolvedSource.LocalPath,
                targetPath,
                options.DryRun,
                (action, target) => renderer.ShowWorkflowAction(action, target, options.DryRun));
            return;
        }

        if (Directory.Exists(resolvedSource.LocalPath))
        {
            throw new InvalidOperationException(
                $"{entryRef.Type.ToTypeString()} '{entryRef.Entry.Name}' source must point to a file.");
        }

        fileSystemService.EnsureFileInstallAllowed(resolvedSource.LocalPath, targetPath, state, entryRef, options.Force);
        renderer.ShowWorkflowAction(File.Exists(targetPath) ? "update" : "install", $"{entryRef.Type.ToTypeString()} {entryRef.Entry.Name}", options.DryRun);
        fileSystemService.CopyFileIfNeeded(
            resolvedSource.LocalPath,
            targetPath,
            options.DryRun,
            overwriteExisting: true,
            (action, target) => renderer.ShowWorkflowAction(action, target, options.DryRun));
    }
}
