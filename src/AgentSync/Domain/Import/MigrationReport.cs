namespace AgentSync.Domain.Import;

internal sealed class MigrationReport
{
    public List<MigrationReportItem> Imported { get; } = [];
    public List<MigrationReportItem> Skipped { get; } = [];
    public List<MigrationReportItem> Unmapped { get; } = [];
}
