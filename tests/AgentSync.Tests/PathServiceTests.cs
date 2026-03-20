using AgentSync.Application.Catalog;
using AgentSync.Domain;
using AgentSync.Domain.Catalog;
using Shouldly;

namespace AgentSync.Tests;

public sealed class PathServiceTests
{
    private readonly PathService _service = new();

    [Fact]
    public void ExpandConfiguredPath_CopilotPath_UsesConfiguredCopilotHome()
    {
        var original = Environment.GetEnvironmentVariable("COPILOT_HOME");
        var fakeCopilotHome = Path.Combine(Path.GetTempPath(), "copilot-home-test");
        Environment.SetEnvironmentVariable("COPILOT_HOME", fakeCopilotHome);

        try
        {
            var expanded = _service.ExpandConfiguredPath("~/.copilot/agents");

            expanded.ShouldBe(Path.Combine(fakeCopilotHome, "agents"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("COPILOT_HOME", original);
        }
    }

    [Fact]
    public void ResolveInstalledTargetPath_RepoAgent_UsesConfiguredFileName()
    {
        var localRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"));
        var context = new CatalogContext(
            "catalog.json",
            localRoot,
            new CatalogDocument(),
            "copilot",
            "repo",
            localRoot,
            new TargetDirectories { Agents = ".github/agents" },
            "state.json");

        var result = _service.ResolveInstalledTargetPath(
            context,
            new CatalogEntryReference(AssetType.Agent, new CatalogEntry { Name = "sample", Source = "assets\\sample.agent.md" }));

        result.ShouldBe(Path.Combine(localRoot, ".github", "agents", "sample.agent.md"));
    }
}
