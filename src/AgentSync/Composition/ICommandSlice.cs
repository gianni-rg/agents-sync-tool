using System.CommandLine;

namespace AgentSync.Composition;

internal interface ICommandSlice
{
    Command Create();
}
