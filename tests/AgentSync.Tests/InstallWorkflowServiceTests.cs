using AgentSync.Domain.State;
using AgentSync.Tests.Support;
using Shouldly;

namespace AgentSync.Tests;

public sealed class InstallWorkflowServiceTests
{
    [Fact]
    public void UseSyncRemove_FileAssetWorkflow_PreservesStateAndTarget()
    {
        var tempRoot = TestFiles.CreateTempDirectory();
        var catalogRoot = Path.Combine(tempRoot, "catalog");
        var localRoot = Path.Combine(tempRoot, "repo");
        var sourceRoot = Path.Combine(catalogRoot, "sources");
        Directory.CreateDirectory(sourceRoot);
        Directory.CreateDirectory(localRoot);

        try
        {
            var sourcePath = Path.Combine(sourceRoot, "sample.agent.md");
            File.WriteAllText(sourcePath, "version-1");
            TestFiles.WriteCatalog(catalogRoot, sourcePath, "sample", "agent");

            var renderer = new TestConsoleRenderer();
            using var composition = new TestComposition(renderer);

            var installExitCode = composition.InstallWorkflowService.Use(
                "sample",
                catalogRoot,
                new DirectoryInfo(localRoot),
                "copilot",
                "repo",
                "agent",
                dryRun: false,
                force: false);

            installExitCode.ShouldBe(0);

            var targetPath = Path.Combine(localRoot, ".github", "agents", "sample.agent.md");
            File.ReadAllText(targetPath).ShouldBe("version-1");

            var statePath = Path.Combine(localRoot, ".agentsync", "copilot-repo-installed.json");
            var installedState = composition.InstallStateRepository.Load(statePath);
            installedState.Items.Count.ShouldBe(1);
            installedState.Items[0].Name.ShouldBe("sample");

            File.WriteAllText(sourcePath, "version-2");

            var syncExitCode = composition.InstallWorkflowService.Sync(
                catalogRoot,
                new DirectoryInfo(localRoot),
                "copilot",
                "repo",
                dryRun: false,
                force: false);

            syncExitCode.ShouldBe(0);
            File.ReadAllText(targetPath).ShouldBe("version-2");

            var removeExitCode = composition.InstallWorkflowService.Remove(
                "sample",
                catalogRoot,
                new DirectoryInfo(localRoot),
                "copilot",
                "repo",
                "agent",
                dryRun: false);

            removeExitCode.ShouldBe(0);
            File.Exists(targetPath).ShouldBeFalse();

            var removedState = composition.InstallStateRepository.Load(statePath);
            removedState.Items.ShouldBeEmpty();
            renderer.Summaries.ShouldContain(x => x.Contains("Installed agent:sample", StringComparison.Ordinal));
            renderer.Summaries.ShouldContain(x => x.Contains("Sync complete", StringComparison.Ordinal));
            renderer.Summaries.ShouldContain(x => x.Contains("Removed agent:sample", StringComparison.Ordinal));
        }
        finally
        {
            TestFiles.DeleteTempDirectory(tempRoot);
        }
    }
}
