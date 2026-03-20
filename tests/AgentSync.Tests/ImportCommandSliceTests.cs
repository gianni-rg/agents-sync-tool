using System.CommandLine;
using AgentSync.Features.Import;
using AgentSync.Tests.Support;
using Shouldly;

namespace AgentSync.Tests;

public sealed class ImportCommandSliceTests
{
    [Fact]
    public void Create_ImportCommand_DoesNotExposeMigrateAlias()
    {
        var renderer = new TestConsoleRenderer();
        using var composition = new TestComposition(renderer);
        var command = new ImportCommandSlice(composition.ImportWorkflowService, renderer).Create();

        command.Aliases.ShouldBeEmpty();
        command.Name.ShouldBe("import");
    }

    [Fact]
    public void RootCommand_MigrateInvocation_IsRejected()
    {
        var renderer = new TestConsoleRenderer();
        using var composition = new TestComposition(renderer);
        var root = new RootCommand();
        root.Subcommands.Add(new ImportCommandSlice(composition.ImportWorkflowService, renderer).Create());

        var parseResult = root.Parse(["migrate"]);

        parseResult.Errors.ShouldNotBeEmpty();
        parseResult.CommandResult.Command.Name.ShouldNotBe("import");
    }
}
