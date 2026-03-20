using System.Text.Json.Serialization;

namespace AgentSync.Domain.Catalog;

internal sealed class CatalogEntry
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; init; } = string.Empty;

    [JsonPropertyName("requires")]
    public List<string>? Requires { get; init; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; init; }
}
