using System.Text.Json.Serialization;

namespace AgentSync.Domain.Catalog;

internal sealed class TargetDirectories
{
    [JsonPropertyName("agents")]
    public string? Agents { get; init; }

    [JsonPropertyName("prompts")]
    public string? Prompts { get; init; }

    [JsonPropertyName("skills")]
    public string? Skills { get; init; }

    [JsonPropertyName("instructions")]
    public string? Instructions { get; init; }
}
