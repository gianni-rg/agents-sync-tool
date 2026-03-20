using AgentSync.Application.Context;
using AgentSync.Application.Installation;
using AgentSync.Application.Catalog;
using AgentSync.Application.State;
using AgentSync.Domain;
using AgentSync.Domain.Catalog;
using AgentSync.Domain.Discovery;
using AgentSync.Domain.Import;
using AgentSync.Domain.Workflows;
using AgentSync.Infrastructure.Discovery;
using AgentSync.Presentation;

namespace AgentSync.Application.Workflows;

internal sealed class ImportWorkflowService(
    CatalogContextFactory catalogContextFactory,
    CatalogRepository catalogRepository,
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
        bool force,
        bool autoAddUnmapped)
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
            var wasAutoAdded = false;
            if (mappedEntry is null)
            {
                if (!autoAddUnmapped)
                {
                    report.Unmapped.Add(new MigrationReportItem(item, "No unique catalog entry matched the discovered asset."));
                    continue;
                }

                try
                {
                    mappedEntry = AddDiscoveredAssetToCatalog(context, item, dryRun);
                    wasAutoAdded = true;
                }
                catch (Exception ex)
                {
                    failures.Add(ex.Message);
                    report.Skipped.Add(new MigrationReportItem(item, ex.Message));
                    continue;
                }
            }

            try
            {
                var importedForItem = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                assetInstallationService.InstallEntryRecursive(context, state, mappedEntry, new ExecutionOptions(dryRun, force), importedForItem);
                foreach (var key in importedForItem)
                {
                    installedThisRun.Add(key);
                }

                var reason = wasAutoAdded
                    ? $"Auto-added to catalog and mapped to {mappedEntry.Type.ToTypeString()}:{mappedEntry.Entry.Name}."
                    : $"Mapped to {mappedEntry.Type.ToTypeString()}:{mappedEntry.Entry.Name}.";
                report.Imported.Add(new MigrationReportItem(item, reason));
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

    private CatalogEntryReference AddDiscoveredAssetToCatalog(CatalogContext context, DiscoveredAsset item, bool dryRun)
    {
        if (context.Catalog.EnumerateEntries().Any(x =>
                x.Type == item.Type
                && x.Entry.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Unable to auto-add {item.Type.ToTypeString()}:{item.Name} because the catalog already contains an entry with that typed name.");
        }

        var generatedEntry = new CatalogEntry
        {
            Name = item.Name,
            Description = $"Imported from {item.Origin}.",
            Source = catalogRepository.NormalizeCatalogSourceForStorage(context.CatalogDirectory, item.Path)
        };

        context.Catalog.AddEntry(item.Type, generatedEntry);

        try
        {
            catalogRepository.Validate(context.Catalog, context.CatalogFilePath);
        }
        catch
        {
            context.Catalog.GetEntries(item.Type).Remove(generatedEntry);
            throw;
        }

        renderer.ShowWorkflowAction("add", $"{item.Type.ToTypeString()}:{item.Name} to {context.CatalogFilePath}", dryRun);

        if (!dryRun)
        {
            catalogRepository.Save(context.CatalogFilePath, context.Catalog);
        }

        return new CatalogEntryReference(item.Type, generatedEntry);
    }
}
