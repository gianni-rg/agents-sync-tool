using System.Text.Json;
using AgentSync.Application.Catalog;
using AgentSync.Domain.State;

namespace AgentSync.Application.State;

internal sealed class InstallStateRepository(JsonSerializerOptions jsonOptions, PathService pathService)
{
    public string ResolveStateFilePath(string scope, string localRoot, string platform)
    {
        var stateRoot = scope.Equals("repo", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(localRoot, ".agentsync")
            : Path.Combine(pathService.GetCopilotHomePath(), ".agentsync-state");

        return Path.Combine(stateRoot, $"{platform}-{scope}-installed.json");
    }

    public InstallState Load(string stateFilePath)
    {
        if (!File.Exists(stateFilePath))
        {
            return new InstallState();
        }

        var json = File.ReadAllText(stateFilePath);
        return JsonSerializer.Deserialize<InstallState>(json, jsonOptions) ?? new InstallState();
    }

    public void Save(string stateFilePath, InstallState state)
    {
        var directory = Path.GetDirectoryName(stateFilePath)
                        ?? throw new InvalidOperationException("Unable to resolve install state directory.");
        Directory.CreateDirectory(directory);
        File.WriteAllText(stateFilePath, JsonSerializer.Serialize(state, jsonOptions));
    }
}
