using AgentSync.Domain;
using AgentSync.Domain.Catalog;
using AgentSync.Domain.Discovery;
using AgentSync.Tests.Support;
using Shouldly;

namespace AgentSync.Tests;

public sealed class DiscoveryAndGitHubTests
{
    [Fact]
    public void TryMapDiscoveredAsset_SourcePathMatch_ReturnsCatalogEntry()
    {
        var tempRoot = TestFiles.CreateTempDirectory();
        var catalogRoot = Path.Combine(tempRoot, "catalog");
        var sourceRoot = Path.Combine(catalogRoot, "sources");
        Directory.CreateDirectory(sourceRoot);

        try
        {
            var sourcePath = Path.Combine(sourceRoot, "mapped.agent.md");
            File.WriteAllText(sourcePath, "content");
            TestFiles.WriteCatalog(catalogRoot, sourcePath, "mapped", "agent");

            var renderer = new TestConsoleRenderer();
            using var composition = new TestComposition(renderer);
            var context = composition.CatalogContextFactory.Create(catalogRoot, new DirectoryInfo(tempRoot), "copilot", "repo");
            var asset = new DiscoveredAsset(AssetType.Agent, "other-name", sourcePath, "repo-agents");

            var result = composition.DiscoveryService.TryMapDiscoveredAsset(context, asset);

            result.ShouldNotBeNull();
            result!.Entry.Name.ShouldBe("mapped");
        }
        finally
        {
            TestFiles.DeleteTempDirectory(tempRoot);
        }
    }

    [Theory]
    [InlineData("https://github.com/octocat/Hello-World/blob/main/.github/agents/sample.agent.md", "File", "octocat", "Hello-World", "main", ".github/agents/sample.agent.md")]
    [InlineData("https://github.com/octocat/Hello-World/tree/main/.github/skills/common", "Directory", "octocat", "Hello-World", "main", ".github/skills/common")]
    [InlineData("https://raw.githubusercontent.com/octocat/Hello-World/main/.github/prompts/sample.prompt.md", "File", "octocat", "Hello-World", "main", ".github/prompts/sample.prompt.md")]
    public void TryParseGitHubSource_SupportedUrls_ReturnExpectedValues(
        string url,
        string expectedType,
        string owner,
        string repository,
        string reference,
        string path)
    {
        var renderer = new TestConsoleRenderer();
        using var composition = new TestComposition(renderer);

        var parsed = composition.GitHubService.TryParseGitHubSource(url, out var source);

        parsed.ShouldBeTrue();
        source.ContentType.ToString().ShouldBe(expectedType);
        source.Owner.ShouldBe(owner);
        source.Repository.ShouldBe(repository);
        source.Reference.ShouldBe(reference);
        source.Path.ShouldBe(path);
    }
}
