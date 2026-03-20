using AgentSync.Domain.Import;

namespace AgentSync.Presentation;

internal interface IConsoleRenderer
{
    void ShowCatalog(string catalogPath, string platform, string scope, IReadOnlyCollection<CatalogListRow> rows);
    void ShowSearch(string catalogPath, string query, IReadOnlyCollection<SearchResultRow> rows);
    void ShowMigrationReport(MigrationReport report, bool dryRun);
    void ShowWorkflowAction(string action, string target, bool dryRun);
    void ShowInfo(string message);
    void ShowSummary(string message);
    void ShowWarning(string message);
    void ShowError(string message);
}
