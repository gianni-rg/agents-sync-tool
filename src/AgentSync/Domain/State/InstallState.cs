using System.Text.Json.Serialization;
using AgentSync.Domain;
using AgentSync.Domain.Catalog;

namespace AgentSync.Domain.State;

internal sealed class InstallState
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 2;

    [JsonPropertyName("items")]
    public List<InstalledCatalogItem> Items { get; set; } = [];

    public bool Contains(AssetType type, string name)
    {
        return Items.Any(x =>
            x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
            && x.Type.Equals(type.ToTypeString(), StringComparison.OrdinalIgnoreCase));
    }

    public void Upsert(CatalogEntryReference entryRef)
    {
        var existing = Items.FirstOrDefault(x =>
            x.Name.Equals(entryRef.Entry.Name, StringComparison.OrdinalIgnoreCase)
            && x.Type.Equals(entryRef.Type.ToTypeString(), StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            Items.Add(new InstalledCatalogItem
            {
                Name = entryRef.Entry.Name,
                Type = entryRef.Type.ToTypeString()
            });
            return;
        }

        existing.Name = entryRef.Entry.Name;
        existing.Type = entryRef.Type.ToTypeString();
    }

    public void Remove(AssetType type, string name)
    {
        Items.RemoveAll(x =>
            x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
            && x.Type.Equals(type.ToTypeString(), StringComparison.OrdinalIgnoreCase));
    }
}
