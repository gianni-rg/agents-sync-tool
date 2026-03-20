using AgentSync.Application.Catalog;
using AgentSync.Domain.Catalog;
using Shouldly;
using AgentSync.Tests.Support;

namespace AgentSync.Tests;

public sealed class CatalogRepositoryTests
{
    [Fact]
    public void Validate_DuplicateTypedEntries_Throws()
    {
        var renderer = new TestConsoleRenderer();
        using var composition = new TestComposition(renderer);
        var catalog = new CatalogDocument
        {
            Catalog = new CatalogCollections
            {
                Agents =
                [
                    new CatalogEntry { Name = "dup", Source = "one.agent.md" },
                    new CatalogEntry { Name = "dup", Source = "two.agent.md" }
                ]
            },
            Targets = new Dictionary<string, Dictionary<string, TargetDirectories>>(StringComparer.OrdinalIgnoreCase)
            {
                ["copilot"] = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["repo"] = new TargetDirectories { Agents = ".github/agents" }
                }
            }
        };

        Should.Throw<InvalidOperationException>(() => composition.CatalogRepository.Validate(catalog, "catalog.json"))
            .Message.ShouldContain("duplicate entry");
    }

    [Fact]
    public void Validate_InvalidDependency_Throws()
    {
        var renderer = new TestConsoleRenderer();
        using var composition = new TestComposition(renderer);
        var catalog = new CatalogDocument
        {
            Catalog = new CatalogCollections
            {
                Agents =
                [
                    new CatalogEntry
                    {
                        Name = "sample",
                        Source = "sample.agent.md",
                        Requires = ["broken-reference"]
                    }
                ]
            },
            Targets = new Dictionary<string, Dictionary<string, TargetDirectories>>(StringComparer.OrdinalIgnoreCase)
            {
                ["copilot"] = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["repo"] = new TargetDirectories { Agents = ".github/agents" }
                }
            }
        };

        Should.Throw<InvalidOperationException>(() => composition.CatalogRepository.Validate(catalog, "catalog.json"))
            .Message.ShouldContain("Invalid typed dependency reference");
    }
}
