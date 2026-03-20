using AgentSync.Application.Catalog;
using AgentSync.Domain;
using AgentSync.Domain.Catalog;
using Shouldly;

namespace AgentSync.Tests;

public sealed class CatalogLookupServiceTests
{
    private readonly CatalogLookupService _service = new();

    [Theory]
    [InlineData("agent", "agent")]
    [InlineData("agents", "agent")]
    [InlineData("prompt", "prompt")]
    [InlineData("skills", "skill")]
    [InlineData("instructions", "instruction")]
    public void ParseAssetType_Alias_ReturnsExpectedType(string input, string expected)
    {
        var result = _service.ParseAssetType(input);

        result.ToTypeString().ShouldBe(expected);
    }

    [Fact]
    public void ResolveEntryReference_DuplicateNameWithoutType_Throws()
    {
        var catalog = new CatalogDocument
        {
            Catalog = new CatalogCollections
            {
                Agents = [new CatalogEntry { Name = "shared", Source = "a.agent.md" }],
                Prompts = [new CatalogEntry { Name = "shared", Source = "b.prompt.md" }]
            }
        };

        Should.Throw<InvalidOperationException>(() => _service.ResolveEntryReference(catalog, "shared", null))
            .Message.ShouldContain("Re-run with --type");
    }

    [Fact]
    public void ResolveTypedReference_ValidReference_ReturnsMatchingEntry()
    {
        var catalog = new CatalogDocument
        {
            Catalog = new CatalogCollections
            {
                Skills = [new CatalogEntry { Name = "common", Source = "skills\\common" }]
            }
        };

        var result = _service.ResolveTypedReference(catalog, "skill:common");

        result.Type.ShouldBe(AssetType.Skill);
        result.Entry.Name.ShouldBe("common");
    }
}
