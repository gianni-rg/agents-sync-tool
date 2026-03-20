using AgentSync.Application.Context;
using AgentSync.Application.State;
using AgentSync.Domain.State;
using AgentSync.Features.List;
using AgentSync.Tests.Support;
using Shouldly;

namespace AgentSync.Tests;

public sealed class ListCommandSliceTests
{
    [Fact]
    public void Invoke_ListCommand_RendersCatalogRowsThroughPresentationLayer()
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
            File.WriteAllText(sourcePath, "content");
            TestFiles.WriteCatalog(catalogRoot, sourcePath, "sample", "agent");

            var renderer = new TestConsoleRenderer();
            using var composition = new TestComposition(renderer);
            var context = composition.CatalogContextFactory.Create(catalogRoot, new DirectoryInfo(localRoot), "copilot", "repo");
            composition.InstallStateRepository.Save(context.StateFilePath, new InstallState
            {
                Items =
                [
                    new InstalledCatalogItem
                    {
                        Name = "sample",
                        Type = "agent"
                    }
                ]
            });

            var command = new ListCommandSlice(composition.CatalogContextFactory, composition.InstallStateRepository, renderer).Create();

            var exitCode = command.Parse(["list", "--catalog", catalogRoot, "--local", localRoot]).Invoke();

            exitCode.ShouldBe(0);
            renderer.CatalogRows.Count.ShouldBe(1);
            renderer.CatalogRows[0].Name.ShouldBe("sample");
            renderer.CatalogRows[0].Installed.ShouldBeTrue();
            renderer.CatalogPath.ShouldNotBeNull();
        }
        finally
        {
            TestFiles.DeleteTempDirectory(tempRoot);
        }
    }
}
