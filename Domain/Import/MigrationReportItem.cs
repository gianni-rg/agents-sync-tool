using AgentSync.Domain.Discovery;

namespace AgentSync.Domain.Import;

internal sealed record MigrationReportItem(DiscoveredAsset Asset, string Reason);
