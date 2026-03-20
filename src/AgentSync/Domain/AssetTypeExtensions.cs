namespace AgentSync.Domain;

internal static class AssetTypeExtensions
{
    public static string ToTypeString(this AssetType type) =>
        type switch
        {
            AssetType.Agent => "agent",
            AssetType.Prompt => "prompt",
            AssetType.Skill => "skill",
            AssetType.Instruction => "instruction",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

    public static string ToPluralDisplayName(this AssetType type) =>
        type switch
        {
            AssetType.Agent => "Agents",
            AssetType.Prompt => "Prompts",
            AssetType.Skill => "Skills",
            AssetType.Instruction => "Instructions",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
}
