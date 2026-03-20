using AgentSync.Application.Catalog;
using AgentSync.Application.Context;
using AgentSync.Domain;
using AgentSync.Application.Installation;
using AgentSync.Application.State;
using AgentSync.Domain.Catalog;
using AgentSync.Domain.Workflows;
using AgentSync.Infrastructure.FileSystem;
using AgentSync.Presentation;

namespace AgentSync.Application.Workflows;

internal sealed class InstallWorkflowService(
    CatalogContextFactory catalogContextFactory,
    InstallStateRepository installStateRepository,
    CatalogLookupService catalogLookupService,
    AssetInstallationService assetInstallationService,
    PathService pathService,
    FileSystemService fileSystemService,
    IConsoleRenderer renderer)
{
    public int Use(
        string name,
        string? catalogInput,
        DirectoryInfo? localPath,
        string? platform,
        string? scope,
        string? type,
        bool dryRun,
        bool force)
    {
        var context = catalogContextFactory.Create(catalogInput, localPath, platform, scope);
        var state = installStateRepository.Load(context.StateFilePath);
        var entryRef = catalogLookupService.ResolveEntryReference(context.Catalog, name, type);
        var installedThisRun = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        assetInstallationService.InstallEntryRecursive(context, state, entryRef, new ExecutionOptions(dryRun, force), installedThisRun);
        assetInstallationService.PersistInstalledEntries(context, state, installedThisRun, dryRun);

        renderer.ShowSummary($"Installed {entryRef.Type.ToTypeString()}:{entryRef.Entry.Name} for platform '{context.Platform}' scope '{context.Scope}'.");
        return 0;
    }

    public int Install(
        IReadOnlyCollection<string> names,
        string? catalogInput,
        DirectoryInfo? localPath,
        string? platform,
        string? scope,
        bool dryRun,
        bool force)
    {
        var context = catalogContextFactory.Create(catalogInput, localPath, platform, scope);
        var state = installStateRepository.Load(context.StateFilePath);
        var installedThisRun = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var entries = names.Count == 0
            ? context.Catalog.EnumerateEntries()
                .OrderBy(x => x.Type.ToTypeString())
                .ThenBy(x => x.Entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : names
                .Select(name => catalogLookupService.ResolveEntryReference(context.Catalog, name, null))
                .DistinctBy(x => $"{x.Type.ToTypeString()}:{x.Entry.Name}", StringComparer.OrdinalIgnoreCase)
                .ToList();

        foreach (var entryRef in entries)
        {
            assetInstallationService.InstallEntryRecursive(context, state, entryRef, new ExecutionOptions(dryRun, force), installedThisRun);
        }

        assetInstallationService.PersistInstalledEntries(context, state, installedThisRun, dryRun);
        renderer.ShowSummary($"Installed {entries.Count} catalog asset(s) for platform '{context.Platform}' scope '{context.Scope}'.");
        return 0;
    }

    public int Sync(
        string? catalogInput,
        DirectoryInfo? localPath,
        string? platform,
        string? scope,
        bool dryRun,
        bool force)
    {
        var context = catalogContextFactory.Create(catalogInput, localPath, platform, scope);
        var state = installStateRepository.Load(context.StateFilePath);
        if (state.Items.Count == 0)
        {
            renderer.ShowInfo("No installed assets tracked for this platform and scope.");
            return 0;
        }

        var installedThisRun = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var failures = new List<string>();

        foreach (var item in state.Items
                     .OrderBy(x => x.Type, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var typedReference = $"{item.Type}:{item.Name}";
            try
            {
                var entryRef = catalogLookupService.ResolveTypedReference(context.Catalog, typedReference);
                assetInstallationService.InstallEntryRecursive(context, state, entryRef, new ExecutionOptions(dryRun, force), installedThisRun);
            }
            catch (Exception ex)
            {
                failures.Add(ex.Message);
                renderer.ShowError($"Failed to sync {typedReference}: {ex.Message}");
            }
        }

        assetInstallationService.PersistInstalledEntries(context, state, installedThisRun, dryRun);
        renderer.ShowSummary($"Sync complete for platform '{context.Platform}' scope '{context.Scope}'.");
        return failures.Count == 0 ? 0 : 1;
    }

    public int Remove(
        string name,
        string? catalogInput,
        DirectoryInfo? localPath,
        string? platform,
        string? scope,
        string? type,
        bool dryRun)
    {
        var context = catalogContextFactory.Create(catalogInput, localPath, platform, scope);
        var state = installStateRepository.Load(context.StateFilePath);
        var entryRef = catalogLookupService.ResolveEntryReference(context.Catalog, name, type);
        var targetPath = pathService.ResolveInstalledTargetPath(context, entryRef);

        if (!File.Exists(targetPath) && !Directory.Exists(targetPath) && !state.Contains(entryRef.Type, entryRef.Entry.Name))
        {
            renderer.ShowInfo($"Nothing to remove for {entryRef.Type.ToTypeString()}:{entryRef.Entry.Name}.");
            return 0;
        }

        fileSystemService.RemovePath(targetPath, dryRun, (action, target) => renderer.ShowWorkflowAction(action, target, dryRun));

        if (!dryRun)
        {
            state.Remove(entryRef.Type, entryRef.Entry.Name);
            installStateRepository.Save(context.StateFilePath, state);
        }

        renderer.ShowSummary($"Removed {entryRef.Type.ToTypeString()}:{entryRef.Entry.Name}.");
        return 0;
    }
}
