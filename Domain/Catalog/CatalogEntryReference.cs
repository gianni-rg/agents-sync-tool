using AgentSync.Domain;

namespace AgentSync.Domain.Catalog;

internal sealed record CatalogEntryReference(AssetType Type, CatalogEntry Entry);
