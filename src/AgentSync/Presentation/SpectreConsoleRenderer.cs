using AgentSync.Domain;
using AgentSync.Domain.Import;
using Spectre.Console;

namespace AgentSync.Presentation;

internal sealed class SpectreConsoleRenderer : IConsoleRenderer
{
    public void ShowCatalog(string catalogPath, string platform, string scope, IReadOnlyCollection<CatalogListRow> rows)
    {
        AnsiConsole.Write(new Rule("[yellow]Catalog[/]"));
        AnsiConsole.MarkupLine($"Path: [grey]{Markup.Escape(catalogPath)}[/]");
        AnsiConsole.MarkupLine($"Platform: [grey]{Markup.Escape(platform)}[/]");
        AnsiConsole.MarkupLine($"Scope: [grey]{Markup.Escape(scope)}[/]");
        AnsiConsole.WriteLine();

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Type");
        table.AddColumn("Name");
        table.AddColumn("Description");
        table.AddColumn("Installed");
        table.AddColumn("Source");
        table.AddColumn("Tags");

        foreach (var row in rows.OrderBy(x => x.Type.ToTypeString()).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            table.AddRow(
                row.Type.ToTypeString(),
                Markup.Escape(row.Name),
                Markup.Escape(row.Description),
                row.Installed ? "[green]yes[/]" : "[grey]no[/]",
                Markup.Escape(row.Source),
                Markup.Escape(row.Tags));
        }

        AnsiConsole.Write(table);
    }

    public void ShowSearch(string catalogPath, string query, IReadOnlyCollection<SearchResultRow> rows)
    {
        AnsiConsole.Write(new Rule("[yellow]Search[/]"));
        AnsiConsole.MarkupLine($"Catalog: [grey]{Markup.Escape(catalogPath)}[/]");
        AnsiConsole.MarkupLine($"Query: [grey]{Markup.Escape(query)}[/]");
        AnsiConsole.WriteLine();

        if (rows.Count == 0)
        {
            ShowInfo("No catalog entries matched.");
            return;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Type");
        table.AddColumn("Name");
        table.AddColumn("Description");
        table.AddColumn("Tags");

        foreach (var row in rows.OrderBy(x => x.Type.ToTypeString()).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            table.AddRow(
                row.Type.ToTypeString(),
                Markup.Escape(row.Name),
                Markup.Escape(row.Description),
                Markup.Escape(row.Tags));
        }

        AnsiConsole.Write(table);
    }

    public void ShowMigrationReport(MigrationReport report, bool dryRun)
    {
        AnsiConsole.Write(new Rule(dryRun ? "[yellow]Dry-run import report[/]" : "[green]Import report[/]"));
        AnsiConsole.MarkupLine($"Imported: [green]{report.Imported.Count}[/]");
        AnsiConsole.MarkupLine($"Skipped: [yellow]{report.Skipped.Count}[/]");
        AnsiConsole.MarkupLine($"Unmapped: [red]{report.Unmapped.Count}[/]");
        AnsiConsole.WriteLine();

        RenderMigrationSection("Imported", report.Imported);
        RenderMigrationSection("Skipped", report.Skipped);
        RenderMigrationSection("Unmapped", report.Unmapped);
    }

    public void ShowWorkflowAction(string action, string target, bool dryRun)
    {
        var prefix = dryRun ? $"Would {action}" : CultureAwareCapitalize(action);
        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(prefix)}[/] {Markup.Escape(target)}");
    }

    public void ShowInfo(string message)
    {
        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(message)}[/]");
    }

    public void ShowSummary(string message)
    {
        AnsiConsole.MarkupLine($"[green]{Markup.Escape(message)}[/]");
    }

    public void ShowWarning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(message)}[/]");
    }

    public void ShowError(string message)
    {
        AnsiConsole.MarkupLine($"[red]{Markup.Escape(message)}[/]");
    }

    private static void RenderMigrationSection(string title, IReadOnlyCollection<MigrationReportItem> items)
    {
        AnsiConsole.Write(new Rule($"[blue]{Markup.Escape(title)}[/]"));

        if (items.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]none[/]");
            AnsiConsole.WriteLine();
            return;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Asset");
        table.AddColumn("Origin");
        table.AddColumn("Reason");

        foreach (var item in items)
        {
            table.AddRow(
                $"{item.Asset.Type.ToTypeString()}:{Markup.Escape(item.Asset.Name)}",
                Markup.Escape(item.Asset.Origin),
                Markup.Escape(item.Reason));
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static string CultureAwareCapitalize(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];
}
