using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AgentSync.Domain;
using AgentSync.Domain.Catalog;
using AgentSync.Domain.Sources;

namespace AgentSync.Infrastructure.GitHub;

internal sealed class GitHubService(HttpClient gitHubClient, JsonSerializerOptions jsonOptions)
{
    public bool TryParseGitHubSource(string source, out GitHubSource gitHubSource)
    {
        gitHubSource = default!;
        if (string.IsNullOrWhiteSpace(source) || !Uri.TryCreate(source, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            && !uri.Host.Equals("raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();

        if (uri.Host.Equals("raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase))
        {
            if (segments.Length < 4)
            {
                throw new InvalidOperationException($"Unsupported GitHub raw URL: {source}");
            }

            gitHubSource = new GitHubSource(
                segments[0],
                segments[1],
                segments[2],
                string.Join("/", segments.Skip(3)),
                GitHubContentType.File,
                source);
            return true;
        }

        if (segments.Length >= 5 && (segments[2].Equals("blob", StringComparison.OrdinalIgnoreCase)
                                     || segments[2].Equals("tree", StringComparison.OrdinalIgnoreCase)))
        {
            gitHubSource = new GitHubSource(
                segments[0],
                segments[1],
                segments[3],
                string.Join("/", segments.Skip(4)),
                segments[2].Equals("tree", StringComparison.OrdinalIgnoreCase) ? GitHubContentType.Directory : GitHubContentType.File,
                source);
            return true;
        }

        throw new InvalidOperationException(
            $"Unsupported GitHub URL: {source}. Use a browser blob/tree URL or a raw.githubusercontent.com URL with an explicit branch and path.");
    }

    public ResolvedAssetSource DownloadSource(CatalogEntryReference entryRef, GitHubSource gitHubSource)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "AgentSync", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(tempRoot);

        if (entryRef.Type == AssetType.Skill)
        {
            var directoryPath = Path.Combine(tempRoot, "content");
            Directory.CreateDirectory(directoryPath);
            DownloadGitHubDirectory(gitHubSource, directoryPath);
            return new ResolvedAssetSource(directoryPath, true, tempRoot);
        }

        var fileName = Path.GetFileName(gitHubSource.Path);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new InvalidOperationException($"GitHub source does not point to a file: {gitHubSource.OriginalUrl}");
        }

        var filePath = Path.Combine(tempRoot, fileName);
        DownloadGitHubFile(gitHubSource, filePath);
        return new ResolvedAssetSource(filePath, true, tempRoot);
    }

    public void Push(
        CatalogEntryReference entryRef,
        GitHubSource gitHubSource,
        string installedPath,
        bool dryRun,
        string? message,
        Action<string, string> reportAction)
    {
        if (!HasGitHubToken())
        {
            throw new InvalidOperationException("GitHub-backed push requires GITHUB_TOKEN or GH_TOKEN.");
        }

        if (entryRef.Type == AssetType.Skill)
        {
            PushDirectoryToGitHub(gitHubSource, installedPath, dryRun, message, entryRef.Entry.Name, reportAction);
            return;
        }

        PushFileToGitHub(gitHubSource, installedPath, dryRun, message, entryRef.Entry.Name, reportAction);
    }

    private void PushFileToGitHub(
        GitHubSource gitHubSource,
        string localFilePath,
        bool dryRun,
        string? message,
        string entryName,
        Action<string, string> reportAction)
    {
        if (!File.Exists(localFilePath))
        {
            throw new InvalidOperationException($"Local file to push was not found: {localFilePath}");
        }

        reportAction("push", $"{localFilePath} -> {gitHubSource.Owner}/{gitHubSource.Repository}@{gitHubSource.Reference}:{gitHubSource.Path}");
        if (dryRun)
        {
            return;
        }

        var content = Convert.ToBase64String(File.ReadAllBytes(localFilePath));
        var sha = GetGitHubContentSha(gitHubSource.Owner, gitHubSource.Repository, gitHubSource.Path, gitHubSource.Reference);
        PutGitHubFile(
            gitHubSource.Owner,
            gitHubSource.Repository,
            gitHubSource.Path,
            gitHubSource.Reference,
            content,
            sha,
            message ?? $"Update {entryName} via AgentSync");
    }

    private void PushDirectoryToGitHub(
        GitHubSource gitHubSource,
        string localDirectoryPath,
        bool dryRun,
        string? message,
        string entryName,
        Action<string, string> reportAction)
    {
        if (!Directory.Exists(localDirectoryPath))
        {
            throw new InvalidOperationException($"Local directory to push was not found: {localDirectoryPath}");
        }

        var files = Directory.EnumerateFiles(localDirectoryPath, "*", SearchOption.AllDirectories)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(localDirectoryPath, file).Replace('\\', '/');
            var remotePath = string.IsNullOrWhiteSpace(gitHubSource.Path)
                ? relativePath
                : $"{gitHubSource.Path.TrimEnd('/')}/{relativePath}";

            reportAction("push", $"{relativePath} -> {gitHubSource.Owner}/{gitHubSource.Repository}@{gitHubSource.Reference}:{remotePath}");
            if (dryRun)
            {
                continue;
            }

            var content = Convert.ToBase64String(File.ReadAllBytes(file));
            var sha = GetGitHubContentSha(gitHubSource.Owner, gitHubSource.Repository, remotePath, gitHubSource.Reference);
            PutGitHubFile(
                gitHubSource.Owner,
                gitHubSource.Repository,
                remotePath,
                gitHubSource.Reference,
                content,
                sha,
                message ?? $"Update {entryName} via AgentSync");
        }
    }

    private string? GetGitHubContentSha(string owner, string repo, string path, string reference)
    {
        var endpoint = BuildGitHubContentsApiUrl(owner, repo, path, reference);
        using var request = CreateGitHubRequest(HttpMethod.Get, endpoint);
        using var response = gitHubClient.Send(request);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        EnsureGitHubSuccess(response, "read GitHub file metadata");
        using var json = JsonDocument.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        return json.RootElement.TryGetProperty("sha", out var shaProperty) ? shaProperty.GetString() : null;
    }

    private void PutGitHubFile(
        string owner,
        string repo,
        string path,
        string reference,
        string base64Content,
        string? sha,
        string message)
    {
        var endpoint = $"https://api.github.com/repos/{owner}/{repo}/contents/{EscapeGitHubPath(path)}";
        var payload = new GitHubPutContentRequest
        {
            Message = message,
            Branch = reference,
            Content = base64Content,
            Sha = sha
        };

        using var request = CreateGitHubRequest(HttpMethod.Put, endpoint);
        request.Content = new StringContent(JsonSerializer.Serialize(payload, jsonOptions), Encoding.UTF8, "application/json");

        using var response = gitHubClient.Send(request);
        EnsureGitHubSuccess(response, "push GitHub content");
    }

    private void DownloadGitHubFile(GitHubSource gitHubSource, string localFilePath)
    {
        using var response = SendGitHubRequest(HttpMethod.Get, BuildGitHubContentsApiUrl(
            gitHubSource.Owner,
            gitHubSource.Repository,
            gitHubSource.Path,
            gitHubSource.Reference));

        EnsureGitHubSuccess(response, "download GitHub file");

        var payload = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        using var json = JsonDocument.Parse(payload);
        if (!json.RootElement.TryGetProperty("type", out var typeElement)
            || !string.Equals(typeElement.GetString(), "file", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"GitHub source is not a file: {gitHubSource.OriginalUrl}");
        }

        var base64 = json.RootElement.GetProperty("content").GetString()
                     ?? throw new InvalidOperationException($"GitHub file content was empty: {gitHubSource.OriginalUrl}");
        var bytes = Convert.FromBase64String(base64.Replace("\n", string.Empty, StringComparison.Ordinal));
        Directory.CreateDirectory(Path.GetDirectoryName(localFilePath)
                                  ?? throw new InvalidOperationException("Unable to create GitHub temp directory."));
        File.WriteAllBytes(localFilePath, bytes);
    }

    private void DownloadGitHubDirectory(GitHubSource gitHubSource, string localDirectoryPath)
    {
        DownloadGitHubDirectoryCore(gitHubSource.Owner, gitHubSource.Repository, gitHubSource.Reference, gitHubSource.Path, localDirectoryPath);
    }

    private void DownloadGitHubDirectoryCore(
        string owner,
        string repo,
        string reference,
        string remotePath,
        string localDirectoryPath)
    {
        using var response = SendGitHubRequest(HttpMethod.Get, BuildGitHubContentsApiUrl(owner, repo, remotePath, reference));
        EnsureGitHubSuccess(response, "download GitHub directory");

        var payload = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        using var json = JsonDocument.Parse(payload);
        if (json.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"GitHub source is not a directory: {remotePath}");
        }

        Directory.CreateDirectory(localDirectoryPath);

        foreach (var item in json.RootElement.EnumerateArray())
        {
            var type = item.GetProperty("type").GetString();
            var name = item.GetProperty("name").GetString()
                       ?? throw new InvalidOperationException("GitHub directory item was missing a name.");
            var path = item.GetProperty("path").GetString()
                       ?? throw new InvalidOperationException("GitHub directory item was missing a path.");

            switch (type)
            {
                case "file":
                {
                    var downloadUrl = item.GetProperty("download_url").GetString();
                    if (string.IsNullOrWhiteSpace(downloadUrl))
                    {
                        var childSource = new GitHubSource(owner, repo, reference, path, GitHubContentType.File, path);
                        DownloadGitHubFile(childSource, Path.Combine(localDirectoryPath, name));
                    }
                    else
                    {
                        DownloadUrlToFile(downloadUrl, Path.Combine(localDirectoryPath, name));
                    }

                    break;
                }
                case "dir":
                    DownloadGitHubDirectoryCore(owner, repo, reference, path, Path.Combine(localDirectoryPath, name));
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported GitHub directory item type '{type}' at '{path}'.");
            }
        }
    }

    private void DownloadUrlToFile(string url, string localFilePath)
    {
        using var response = SendGitHubRequest(HttpMethod.Get, url);
        EnsureGitHubSuccess(response, "download GitHub raw content");
        Directory.CreateDirectory(Path.GetDirectoryName(localFilePath)
                                  ?? throw new InvalidOperationException("Unable to create GitHub temp directory."));
        File.WriteAllBytes(localFilePath, response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult());
    }

    private HttpResponseMessage SendGitHubRequest(HttpMethod method, string url)
    {
        using var request = CreateGitHubRequest(method, url);
        return gitHubClient.Send(request);
    }

    private HttpRequestMessage CreateGitHubRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("AgentSync", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        var token = GetGitHubToken();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    private static string BuildGitHubContentsApiUrl(string owner, string repo, string path, string reference)
    {
        var encodedPath = EscapeGitHubPath(path);
        var encodedRef = Uri.EscapeDataString(reference);
        return $"https://api.github.com/repos/{owner}/{repo}/contents/{encodedPath}?ref={encodedRef}";
    }

    private static string EscapeGitHubPath(string path)
    {
        return string.Join("/", path
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString));
    }

    private static void EnsureGitHubSuccess(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException(
                $"Unable to {operation}. GitHub denied access. Provide GITHUB_TOKEN or GH_TOKEN for private or rate-limited sources. Response: {responseBody}");
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(
                $"Unable to {operation}. The GitHub source was not found or is private. Response: {responseBody}");
        }

        throw new InvalidOperationException($"Unable to {operation}. GitHub returned {(int)response.StatusCode} {response.StatusCode}. Response: {responseBody}");
    }

    private bool HasGitHubToken() => !string.IsNullOrWhiteSpace(GetGitHubToken());

    private static string? GetGitHubToken()
    {
        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrWhiteSpace(token))
        {
            return token;
        }

        return Environment.GetEnvironmentVariable("GH_TOKEN");
    }
}
