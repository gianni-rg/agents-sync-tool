using System.Text.Json.Serialization;

namespace AgentSync.Domain.Catalog;

internal sealed class CatalogCollections
{
    [JsonPropertyName("agents")]
    public List<CatalogEntry> Agents { get; init; } = [];

    [JsonPropertyName("prompts")]
    public List<CatalogEntry> Prompts { get; init; } = [];

    [JsonPropertyName("skills")]
    public List<CatalogEntry> Skills { get; init; } = [];

    [JsonPropertyName("instructions")]
    public List<CatalogEntry> Instructions { get; init; } = [];
}
