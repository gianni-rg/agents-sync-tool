using AgentSync.Domain;

namespace AgentSync.Domain.Sources;

internal sealed record GitHubSource(
    string Owner,
    string Repository,
    string Reference,
    string Path,
    GitHubContentType ContentType,
    string OriginalUrl);
