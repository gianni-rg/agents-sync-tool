namespace AgentSync.Domain.Sources;

internal sealed class ResolvedAssetSource : IDisposable
{
    public ResolvedAssetSource(string localPath, bool isTemporary, string? tempRoot = null)
    {
        LocalPath = localPath;
        IsTemporary = isTemporary;
        TempRoot = tempRoot;
    }

    public string LocalPath { get; }
    public bool IsTemporary { get; }
    public string? TempRoot { get; }

    public void Dispose()
    {
        if (!IsTemporary || string.IsNullOrWhiteSpace(TempRoot) || !Directory.Exists(TempRoot))
        {
            return;
        }

        try
        {
            Directory.Delete(TempRoot, recursive: true);
        }
        catch
        {
        }
    }
}
