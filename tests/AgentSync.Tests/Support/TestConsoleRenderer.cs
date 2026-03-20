using AgentSync.Domain.Import;
using AgentSync.Presentation;

namespace AgentSync.Tests.Support;

internal sealed class TestConsoleRenderer : IConsoleRenderer
{
    public List<CatalogListRow> CatalogRows { get; } = [];
    public List<SearchResultRow> SearchRows { get; } = [];
    public List<(string Action, string Target, bool DryRun)> WorkflowActions { get; } = [];
    public List<string> Infos { get; } = [];
    public List<string> Summaries { get; } = [];
    public List<string> Warnings { get; } = [];
    public List<string> Errors { get; } = [];
    public MigrationReport? MigrationReport { get; private set; }
    public bool? MigrationDryRun { get; private set; }
    public string? CatalogPath { get; private set; }
    public string? Platform { get; private set; }
    public string? Scope { get; private set; }
    public string? SearchCatalogPath { get; private set; }
    public string? SearchQuery { get; private set; }

    public void ShowCatalog(string catalogPath, string platform, string scope, IReadOnlyCollection<CatalogListRow> rows)
    {
        CatalogPath = catalogPath;
        Platform = platform;
        Scope = scope;
        CatalogRows.Clear();
        CatalogRows.AddRange(rows);
    }

    public void ShowSearch(string catalogPath, string query, IReadOnlyCollection<SearchResultRow> rows)
    {
        SearchCatalogPath = catalogPath;
        SearchQuery = query;
        SearchRows.Clear();
        SearchRows.AddRange(rows);
    }

    public void ShowMigrationReport(MigrationReport report, bool dryRun)
    {
        MigrationReport = report;
        MigrationDryRun = dryRun;
    }

    public void ShowWorkflowAction(string action, string target, bool dryRun)
    {
        WorkflowActions.Add((action, target, dryRun));
    }

    public void ShowInfo(string message)
    {
        Infos.Add(message);
    }

    public void ShowSummary(string message)
    {
        Summaries.Add(message);
    }

    public void ShowWarning(string message)
    {
        Warnings.Add(message);
    }

    public void ShowError(string message)
    {
        Errors.Add(message);
    }
}
