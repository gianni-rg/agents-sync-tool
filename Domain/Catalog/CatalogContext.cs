namespace AgentSync.Domain.Catalog;

internal sealed record CatalogContext(
    string CatalogFilePath,
    string CatalogDirectory,
    CatalogDocument Catalog,
    string Platform,
    string Scope,
    string LocalRoot,
    TargetDirectories TargetDirectories,
    string StateFilePath);
