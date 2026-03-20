using AgentSync.Domain;

namespace AgentSync.Presentation;

internal sealed record CatalogListRow(
    AssetType Type,
    string Name,
    string Description,
    bool Installed,
    string Source,
    string Tags);
