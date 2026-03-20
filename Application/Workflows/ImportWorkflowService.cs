using AgentSync.Application.Context;
using AgentSync.Application.Installation;
using AgentSync.Application.State;
using AgentSync.Domain;
using AgentSync.Domain.Import;
using AgentSync.Domain.Workflows;
using AgentSync.Infrastructure.Discovery;
using AgentSync.Presentation;

namespace AgentSync.Application.Workflows;

internal sealed class ImportWorkflowService(
    CatalogContextFactory catalogContextFactory,
    InstallStateRepository installStateRepository,
    DiscoveryService discoveryService,
    AssetInstallationService assetInstallationService,
    IConsoleRenderer renderer)
{
    public int Import(
        string? catalogInput,
        DirectoryInfo? localPath,
        string? platform,
        string? scope,
        string? source,
        bool dryRun,
        bool force)
    {
        var context = catalogContextFactory.Create(catalogInput, localPath, platform, scope);
        var state = installStateRepository.Load(context.StateFilePath);
        var discovered = discoveryService.DiscoverUnmanagedAssets(context, source);
        if (discovered.Count == 0)
        {
            renderer.ShowInfo("No unmanaged assets were discovered.");
            return 0;
        }

        var report = new MigrationReport();
        var installedThisRun = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var failures = new List<string>();

        foreach (var item in discovered
                     .OrderBy(x => x.Type.ToTypeString())
                     .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase))
        {
            if (state.Contains(item.Type, item.Name) || installedThisRun.Contains($"{item.Type.ToTypeString()}:{item.Name}"))
            {
                report.Skipped.Add(new MigrationReportItem(item, "Already managed."));
                continue;
            }

            var mappedEntry = discoveryService.TryMapDiscoveredAsset(context, item);
            if (mappedEntry is null)
            {
                report.Unmapped.Add(new MigrationReportItem(item, "No unique catalog entry matched the discovered asset."));
                continue;
            }

            try
            {
                var importedForItem = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                assetInstallationService.InstallEntryRecursive(context, state, mappedEntry, new ExecutionOptions(dryRun, force), importedForItem);
                foreach (var key in importedForItem)
                {
                    installedThisRun.Add(key);
                }

                report.Imported.Add(new MigrationReportItem(item, $"Mapped to {mappedEntry.Type.ToTypeString()}:{mappedEntry.Entry.Name}."));
            }
            catch (Exception ex)
            {
                failures.Add(ex.Message);
                report.Skipped.Add(new MigrationReportItem(item, ex.Message));
            }
        }

        assetInstallationService.PersistInstalledEntries(context, state, installedThisRun, dryRun);
        renderer.ShowMigrationReport(report, dryRun);
        return failures.Count == 0 ? 0 : 1;
    }
}
