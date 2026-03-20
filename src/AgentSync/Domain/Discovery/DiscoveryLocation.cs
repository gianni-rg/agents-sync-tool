using AgentSync.Domain;

namespace AgentSync.Domain.Discovery;

internal sealed record DiscoveryLocation(string Label, AssetType Type, string Root);
