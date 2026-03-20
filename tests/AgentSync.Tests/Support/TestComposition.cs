using System.Text.Json;
using AgentSync.Application.Catalog;
using AgentSync.Application.Context;
using AgentSync.Application.Installation;
using AgentSync.Application.State;
using AgentSync.Application.Workflows;
using AgentSync.Infrastructure.Discovery;
using AgentSync.Infrastructure.FileSystem;
using AgentSync.Infrastructure.GitHub;
using AgentSync.Infrastructure.Sources;

namespace AgentSync.Tests.Support;

internal sealed class TestComposition : IDisposable
{
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    public TestComposition(TestConsoleRenderer renderer)
    {
        JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true
        };

        CatalogLookupService = new CatalogLookupService();
        PathService = new PathService();
        LocalSourceService = new LocalSourceService(PathService);
        CatalogRepository = new CatalogRepository(JsonOptions, PathService, CatalogLookupService);
        InstallStateRepository = new InstallStateRepository(JsonOptions, PathService);
        CatalogContextFactory = new CatalogContextFactory(CatalogRepository, InstallStateRepository);
        FileSystemService = new FileSystemService();
        GitHubService = new GitHubService(_httpClient, JsonOptions);
        AssetSourceResolver = new AssetSourceResolver(GitHubService, LocalSourceService);
        DiscoveryService = new DiscoveryService(PathService, LocalSourceService);
        AssetInstallationService = new AssetInstallationService(
            CatalogLookupService,
            AssetSourceResolver,
            PathService,
            FileSystemService,
            InstallStateRepository,
            renderer);
        InstallWorkflowService = new InstallWorkflowService(
            CatalogContextFactory,
            InstallStateRepository,
            CatalogLookupService,
            AssetInstallationService,
            PathService,
            FileSystemService,
            renderer);
        ImportWorkflowService = new ImportWorkflowService(
            CatalogContextFactory,
            InstallStateRepository,
            DiscoveryService,
            AssetInstallationService,
            renderer);
        PushWorkflowService = new PushWorkflowService(
            CatalogContextFactory,
            CatalogLookupService,
            PathService,
            LocalSourceService,
            FileSystemService,
            GitHubService,
            renderer);
    }

    public JsonSerializerOptions JsonOptions { get; }
    public CatalogLookupService CatalogLookupService { get; }
    public PathService PathService { get; }
    public LocalSourceService LocalSourceService { get; }
    public CatalogRepository CatalogRepository { get; }
    public InstallStateRepository InstallStateRepository { get; }
    public CatalogContextFactory CatalogContextFactory { get; }
    public FileSystemService FileSystemService { get; }
    public GitHubService GitHubService { get; }
    public AssetSourceResolver AssetSourceResolver { get; }
    public DiscoveryService DiscoveryService { get; }
    public AssetInstallationService AssetInstallationService { get; }
    public InstallWorkflowService InstallWorkflowService { get; }
    public ImportWorkflowService ImportWorkflowService { get; }
    public PushWorkflowService PushWorkflowService { get; }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
