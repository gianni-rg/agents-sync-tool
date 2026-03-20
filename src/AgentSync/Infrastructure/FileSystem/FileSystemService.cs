using AgentSync.Domain.Catalog;
using AgentSync.Domain.State;

namespace AgentSync.Infrastructure.FileSystem;

internal sealed class FileSystemService
{
    public void EnsureFileInstallAllowed(
        string sourceFile,
        string targetFile,
        InstallState state,
        CatalogEntryReference entryRef,
        bool force)
    {
        if (!File.Exists(targetFile))
        {
            return;
        }

        if (state.Contains(entryRef.Type, entryRef.Entry.Name))
        {
            return;
        }

        if (FilesAreEqual(sourceFile, targetFile))
        {
            return;
        }

        if (!force)
        {
            throw new InvalidOperationException(
                $"Refusing to overwrite unmanaged file '{targetFile}'. Re-run with --force to allow it.");
        }
    }

    public void EnsureDirectoryInstallAllowed(
        string targetDirectory,
        InstallState state,
        CatalogEntryReference entryRef,
        bool force)
    {
        if (!Directory.Exists(targetDirectory))
        {
            return;
        }

        if (state.Contains(entryRef.Type, entryRef.Entry.Name))
        {
            return;
        }

        if (!Directory.EnumerateFileSystemEntries(targetDirectory).Any())
        {
            return;
        }

        if (!force)
        {
            throw new InvalidOperationException(
                $"Refusing to overwrite unmanaged directory '{targetDirectory}'. Re-run with --force to allow it.");
        }
    }

    public void SyncDirectory(string sourceDirectory, string targetDirectory, bool dryRun, Action<string, string> reportAction)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            throw new InvalidOperationException($"Source directory not found: {sourceDirectory}");
        }

        Directory.CreateDirectory(targetDirectory);

        var sourceFiles = Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)
            .ToDictionary(
                file => Path.GetRelativePath(sourceDirectory, file),
                file => file,
                StringComparer.OrdinalIgnoreCase);

        foreach (var pair in sourceFiles.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var targetFile = Path.Combine(targetDirectory, pair.Key);
            CopyFileIfNeeded(pair.Value, targetFile, dryRun, overwriteExisting: true, reportAction);
        }

        var targetFiles = Directory.EnumerateFiles(targetDirectory, "*", SearchOption.AllDirectories).ToList();
        foreach (var targetFile in targetFiles)
        {
            var relativePath = Path.GetRelativePath(targetDirectory, targetFile);
            if (sourceFiles.ContainsKey(relativePath))
            {
                continue;
            }

            reportAction("delete", targetFile);
            if (!dryRun)
            {
                File.Delete(targetFile);
            }
        }
    }

    public void CopyFileIfNeeded(
        string sourceFile,
        string targetFile,
        bool dryRun,
        bool overwriteExisting,
        Action<string, string> reportAction)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetFile)
                                  ?? throw new InvalidOperationException($"Unable to resolve target directory for {targetFile}."));

        var action = File.Exists(targetFile)
            ? (FilesAreEqual(sourceFile, targetFile) ? "skip" : "update")
            : "copy";

        reportAction(action, targetFile);
        if (action == "skip")
        {
            return;
        }

        if (!dryRun)
        {
            File.Copy(sourceFile, targetFile, overwriteExisting);
            File.SetLastWriteTimeUtc(targetFile, File.GetLastWriteTimeUtc(sourceFile));
        }
    }

    public void RemovePath(string path, bool dryRun, Action<string, string> reportAction)
    {
        if (File.Exists(path))
        {
            reportAction("delete", path);
            if (!dryRun)
            {
                File.Delete(path);
            }

            return;
        }

        if (Directory.Exists(path))
        {
            reportAction("delete", path);
            if (!dryRun)
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }

    public bool FilesAreEqual(string leftPath, string rightPath)
    {
        var leftInfo = new FileInfo(leftPath);
        var rightInfo = new FileInfo(rightPath);
        if (!leftInfo.Exists || !rightInfo.Exists || leftInfo.Length != rightInfo.Length)
        {
            return false;
        }

        using var left = File.OpenRead(leftPath);
        using var right = File.OpenRead(rightPath);
        var leftBuffer = new byte[81920];
        var rightBuffer = new byte[81920];

        while (true)
        {
            var leftRead = left.Read(leftBuffer);
            var rightRead = right.Read(rightBuffer);
            if (leftRead != rightRead)
            {
                return false;
            }

            if (leftRead == 0)
            {
                return true;
            }

            for (var i = 0; i < leftRead; i++)
            {
                if (leftBuffer[i] != rightBuffer[i])
                {
                    return false;
                }
            }
        }
    }
}
