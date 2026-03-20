using System.Text.Json;
using AgentSync.Domain.Catalog;
using AgentSync.Tests.Support;
using Shouldly;

namespace AgentSync.Tests;

public sealed class ImportWorkflowServiceTests
{
    [Fact]
    public void Import_UnmappedAssetWithoutAutoAdd_ReportsUnmappedWithoutPersisting()
    {
        var tempRoot = TestFiles.CreateTempDirectory();
        var catalogRoot = Path.Combine(tempRoot, "catalog");
        var localRoot = Path.Combine(tempRoot, "repo");
        var importRoot = Path.Combine(tempRoot, "unmanaged");
        Directory.CreateDirectory(localRoot);
        Directory.CreateDirectory(Path.Combine(importRoot, "agents"));

        try
        {
            File.WriteAllText(Path.Combine(importRoot, "agents", "sample.agent.md"), "content");
            WriteEmptyCatalog(catalogRoot);

            var renderer = new TestConsoleRenderer();
            using var composition = new TestComposition(renderer);

            var exitCode = composition.ImportWorkflowService.Import(
                catalogRoot,
                new DirectoryInfo(localRoot),
                "copilot",
                "repo",
                importRoot,
                dryRun: false,
                force: false,
                autoAddUnmapped: false);

            exitCode.ShouldBe(0);
            renderer.MigrationReport.ShouldNotBeNull();
            renderer.MigrationReport!.Unmapped.Count.ShouldBe(1);
            renderer.MigrationReport.Imported.ShouldBeEmpty();

            var catalog = composition.CatalogRepository.Load(Path.Combine(catalogRoot, "catalog.json"));
            catalog.Catalog.Agents.ShouldBeEmpty();
            var state = composition.InstallStateRepository.Load(Path.Combine(localRoot, ".agentsync", "copilot-repo-installed.json"));
            state.Items.ShouldBeEmpty();
        }
        finally
        {
            TestFiles.DeleteTempDirectory(tempRoot);
        }
    }

    [Fact]
    public void Import_AutoAddUnmapped_PersistsCatalogAndStateAndInstallsAsset()
    {
        var tempRoot = TestFiles.CreateTempDirectory();
        var catalogRoot = Path.Combine(tempRoot, "catalog");
        var localRoot = Path.Combine(tempRoot, "repo");
        var importRoot = Path.Combine(tempRoot, "unmanaged");
        Directory.CreateDirectory(localRoot);
        Directory.CreateDirectory(Path.Combine(importRoot, "agents"));

        try
        {
            var sourcePath = Path.Combine(importRoot, "agents", "sample.agent.md");
            File.WriteAllText(sourcePath, "content");
            WriteEmptyCatalog(catalogRoot);

            var renderer = new TestConsoleRenderer();
            using var composition = new TestComposition(renderer);

            var exitCode = composition.ImportWorkflowService.Import(
                catalogRoot,
                new DirectoryInfo(localRoot),
                "copilot",
                "repo",
                importRoot,
                dryRun: false,
                force: false,
                autoAddUnmapped: true);

            exitCode.ShouldBe(0);
            renderer.MigrationReport.ShouldNotBeNull();
            renderer.MigrationReport!.Imported.Count.ShouldBe(1);
            renderer.MigrationReport.Imported[0].Reason.ShouldContain("Auto-added");

            var catalog = composition.CatalogRepository.Load(Path.Combine(catalogRoot, "catalog.json"));
            catalog.Catalog.Agents.Count.ShouldBe(1);
            catalog.Catalog.Agents[0].Name.ShouldBe("sample");
            catalog.Catalog.Agents[0].Source.ShouldBe(Path.GetRelativePath(catalogRoot, sourcePath));

            var state = composition.InstallStateRepository.Load(Path.Combine(localRoot, ".agentsync", "copilot-repo-installed.json"));
            state.Items.Count.ShouldBe(1);
            state.Items[0].Name.ShouldBe("sample");

            File.ReadAllText(Path.Combine(localRoot, ".github", "agents", "sample.agent.md")).ShouldBe("content");
        }
        finally
        {
            TestFiles.DeleteTempDirectory(tempRoot);
        }
    }

    [Fact]
    public void Import_AutoAddUnmappedDryRun_ReportsPlannedAdditionWithoutPersisting()
    {
        var tempRoot = TestFiles.CreateTempDirectory();
        var catalogRoot = Path.Combine(tempRoot, "catalog");
        var localRoot = Path.Combine(tempRoot, "repo");
        var importRoot = Path.Combine(tempRoot, "unmanaged");
        Directory.CreateDirectory(localRoot);
        Directory.CreateDirectory(Path.Combine(importRoot, "agents"));

        try
        {
            File.WriteAllText(Path.Combine(importRoot, "agents", "sample.agent.md"), "content");
            WriteEmptyCatalog(catalogRoot);

            var renderer = new TestConsoleRenderer();
            using var composition = new TestComposition(renderer);

            var exitCode = composition.ImportWorkflowService.Import(
                catalogRoot,
                new DirectoryInfo(localRoot),
                "copilot",
                "repo",
                importRoot,
                dryRun: true,
                force: false,
                autoAddUnmapped: true);

            exitCode.ShouldBe(0);
            renderer.MigrationReport.ShouldNotBeNull();
            renderer.MigrationReport!.Imported.Count.ShouldBe(1);
            renderer.MigrationDryRun.ShouldBe(true);
            renderer.WorkflowActions.ShouldContain(x => x.Action == "add" && x.DryRun);

            var catalog = composition.CatalogRepository.Load(Path.Combine(catalogRoot, "catalog.json"));
            catalog.Catalog.Agents.ShouldBeEmpty();
            File.Exists(Path.Combine(localRoot, ".agentsync", "copilot-repo-installed.json")).ShouldBeFalse();
            File.Exists(Path.Combine(localRoot, ".github", "agents", "sample.agent.md")).ShouldBeFalse();
        }
        finally
        {
            TestFiles.DeleteTempDirectory(tempRoot);
        }
    }

    [Fact]
    public void Import_AutoAddUnmappedDuplicateDiscoveredName_ImportsOnceAndSkipsCollision()
    {
        var tempRoot = TestFiles.CreateTempDirectory();
        var catalogRoot = Path.Combine(tempRoot, "catalog");
        var localRoot = Path.Combine(tempRoot, "repo");
        var importRoot = Path.Combine(tempRoot, "unmanaged");
        Directory.CreateDirectory(localRoot);
        Directory.CreateDirectory(Path.Combine(importRoot, "agents"));
        Directory.CreateDirectory(Path.Combine(importRoot, "agents", "nested"));

        try
        {
            File.WriteAllText(Path.Combine(importRoot, "agents", "sample.agent.md"), "content-1");
            File.WriteAllText(Path.Combine(importRoot, "agents", "nested", "sample.agent.md"), "content-2");
            WriteEmptyCatalog(catalogRoot);

            var renderer = new TestConsoleRenderer();
            using var composition = new TestComposition(renderer);

            var exitCode = composition.ImportWorkflowService.Import(
                catalogRoot,
                new DirectoryInfo(localRoot),
                "copilot",
                "repo",
                importRoot,
                dryRun: false,
                force: false,
                autoAddUnmapped: true);

            exitCode.ShouldBe(0);
            renderer.MigrationReport.ShouldNotBeNull();
            renderer.MigrationReport!.Imported.Count.ShouldBe(1);
            renderer.MigrationReport.Skipped.Count.ShouldBe(1);
            renderer.MigrationReport.Skipped[0].Reason.ShouldContain("Already managed");

            var catalog = composition.CatalogRepository.Load(Path.Combine(catalogRoot, "catalog.json"));
            catalog.Catalog.Agents.Count.ShouldBe(1);
        }
        finally
        {
            TestFiles.DeleteTempDirectory(tempRoot);
        }
    }

    private static void WriteEmptyCatalog(string catalogRoot)
    {
        Directory.CreateDirectory(catalogRoot);
        var catalog = new CatalogDocument
        {
            Targets = new Dictionary<string, Dictionary<string, TargetDirectories>>(StringComparer.OrdinalIgnoreCase)
            {
                ["copilot"] = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["repo"] = new TargetDirectories
                    {
                        Agents = ".github/agents",
                        Prompts = ".github/prompts",
                        Skills = ".github/skills",
                        Instructions = ".github/instructions"
                    }
                }
            }
        };

        File.WriteAllText(
            Path.Combine(catalogRoot, "catalog.json"),
            JsonSerializer.Serialize(catalog, new JsonSerializerOptions { WriteIndented = true }));
    }
}
