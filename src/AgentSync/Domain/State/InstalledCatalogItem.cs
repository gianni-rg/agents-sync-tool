using System.Text.Json.Serialization;

namespace AgentSync.Domain.State;

internal sealed class InstalledCatalogItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}
