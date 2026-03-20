using AgentSync.Domain;

namespace AgentSync.Domain.Discovery;

internal sealed record DiscoveredAsset(AssetType Type, string Name, string Path, string Origin);
