using System.Text.Json;
using AgentSync.Application.Catalog;
using AgentSync.Application.Context;
using AgentSync.Application.Installation;
using AgentSync.Application.State;
using AgentSync.Application.Workflows;
using AgentSync.Features.Add;
using AgentSync.Features.Import;
using AgentSync.Features.Install;
using AgentSync.Features.List;
using AgentSync.Features.Push;
using AgentSync.Features.Remove;
using AgentSync.Features.Search;
using AgentSync.Features.Sync;
using AgentSync.Features.Use;
using AgentSync.Infrastructure.Discovery;
using AgentSync.Infrastructure.FileSystem;
using AgentSync.Infrastructure.Sources;
using AgentSync.Infrastructure.GitHub;
using AgentSync.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSync.Composition;

internal static class ServiceCollectionExtensions
{
    public static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true
        });

        services.AddSingleton(new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        });

        services.AddSingleton<IConsoleRenderer, SpectreConsoleRenderer>();

        services.AddSingleton<CatalogLookupService>();
        services.AddSingleton<PathService>();
        services.AddSingleton<LocalSourceService>();
        services.AddSingleton<CatalogRepository>();
        services.AddSingleton<InstallStateRepository>();
        services.AddSingleton<CatalogContextFactory>();
        services.AddSingleton<FileSystemService>();
        services.AddSingleton<GitHubService>();
        services.AddSingleton<AssetSourceResolver>();
        services.AddSingleton<DiscoveryService>();
        services.AddSingleton<AssetInstallationService>();
        services.AddSingleton<InstallWorkflowService>();
        services.AddSingleton<ImportWorkflowService>();
        services.AddSingleton<PushWorkflowService>();

        services.AddSingleton<ICommandSlice, ListCommandSlice>();
        services.AddSingleton<ICommandSlice, SearchCommandSlice>();
        services.AddSingleton<ICommandSlice, UseCommandSlice>();
        services.AddSingleton<ICommandSlice, InstallCommandSlice>();
        services.AddSingleton<ICommandSlice, SyncCommandSlice>();
        services.AddSingleton<ICommandSlice, ImportCommandSlice>();
        services.AddSingleton<ICommandSlice, RemoveCommandSlice>();
        services.AddSingleton<ICommandSlice, AddCommandSlice>();
        services.AddSingleton<ICommandSlice, PushCommandSlice>();

        services.AddSingleton<RootCommandFactory>();

        return services.BuildServiceProvider();
    }
}
