using AgentSync.Domain;

namespace AgentSync.Presentation;

internal sealed record SearchResultRow(
    AssetType Type,
    string Name,
    string Description,
    string Tags);
