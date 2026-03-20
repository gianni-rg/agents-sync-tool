using AgentSync.Composition;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSync;

internal sealed class Program
{
    private static int Main(string[] args)
    {
        using var services = ServiceCollectionExtensions.CreateServiceProvider();
        var rootCommand = services.GetRequiredService<RootCommandFactory>().Create();
        return rootCommand.Parse(args).Invoke();
    }
}
