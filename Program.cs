using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;

internal enum AssetType
{
    Agent,
    Prompt,
    Skill,
    Instruction
}

internal sealed class Program
{
    private static readonly string[] DefaultPatterns = ["*.agent.md", "*.prompt.md"];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    private static int Main(string[] args)
    {
        var rootCommand = new RootCommand("Manage catalog-driven Copilot assets and legacy folder sync workflows.");

        var defaultSyncCommand = new Command("default", "Sync agents from the VS Code user folder to the Copilot user folder.");

        var legacyCatalogPathOption = new Option<DirectoryInfo?>("--catalog", "-c")
        {
            Description = "Path to the legacy source catalog root. Defaults to VS Code or GitHub Copilot user folders if omitted.",
            Required = false
        };

        var legacyLocalPathOption = new Option<DirectoryInfo?>("--local", "-l")
        {
            Description = "Local repo path to sync into (default: current directory).",
            Required = false
        };

        var dryRunOption = new Option<bool>("--dry-run", "-d")
        {
            Description = "Show what would happen without making changes."
        };

        var defaultDryRunOption = new Option<bool>("--dry-run", "-d")
        {
            Description = "Show what would happen without making changes."
        };

        var patternsOption = new Option<string[]>("--pattern", "-p")
        {
            Description = "Legacy file pattern(s) to sync. Repeat --pattern for multiple values. Default: *.agent.md and *.prompt.md.",
            Required = false
        };

        var catalogInputOption = new Option<string?>("--catalog", "-c")
        {
            Description = "Path to catalog.json or the catalog repository root. Defaults to ./catalog.json if present.",
            Required = false
        };

        var localPathOption = new Option<DirectoryInfo?>("--local", "-l")
        {
            Description = "Local repo path. Defaults to the current directory for repo-scoped operations.",
            Required = false
        };

        var platformOption = new Option<string?>("--platform")
        {
            Description = "Target platform from catalog.json. Default: copilot.",
            Required = false
        };

        var scopeOption = new Option<string?>("--scope")
        {
            Description = "Target scope from catalog.json. Default: repo.",
            Required = false
        };

        var typeOption = new Option<string?>("--type", "-t")
        {
            Description = "Asset type: agent, prompt, skill, or instruction.",
            Required = false
        };

        var listCommand = new Command("list", "List catalog entries and their install status.");
        listCommand.Options.Add(catalogInputOption);
        listCommand.Options.Add(localPathOption);
        listCommand.Options.Add(platformOption);
        listCommand.Options.Add(scopeOption);
        listCommand.SetAction(parseResult =>
        {
            try
            {
                return HandleList(
                    parseResult.GetValue(catalogInputOption),
                    parseResult.GetValue(localPathOption),
                    parseResult.GetValue(platformOption),
                    parseResult.GetValue(scopeOption));
            }
            catch (Exception ex)
            {
                return WriteError(ex);
            }
        });

        var useCommand = new Command("use", "Install one catalog asset and its dependencies.");
        var useNameArgument = new Argument<string>("name")
        {
            Description = "Catalog entry name."
        };
        useCommand.Arguments.Add(useNameArgument);
        useCommand.Options.Add(catalogInputOption);
        useCommand.Options.Add(localPathOption);
        useCommand.Options.Add(platformOption);
        useCommand.Options.Add(scopeOption);
        useCommand.Options.Add(typeOption);
        useCommand.Options.Add(dryRunOption);
        useCommand.SetAction(parseResult =>
        {
            try
            {
                return HandleUse(
                    parseResult.GetValue(useNameArgument)!,
                    parseResult.GetValue(catalogInputOption),
                    parseResult.GetValue(localPathOption),
                    parseResult.GetValue(platformOption),
                    parseResult.GetValue(scopeOption),
                    parseResult.GetValue(typeOption),
                    parseResult.GetValue(dryRunOption));
            }
            catch (Exception ex)
            {
                return WriteError(ex);
            }
        });

        var syncCommand = new Command("sync", "Refresh every asset previously installed by AgentSync for the selected platform and scope.");
        syncCommand.Options.Add(catalogInputOption);
        syncCommand.Options.Add(localPathOption);
        syncCommand.Options.Add(platformOption);
        syncCommand.Options.Add(scopeOption);
        syncCommand.Options.Add(dryRunOption);
        syncCommand.SetAction(parseResult =>
        {
            try
            {
                return HandleSync(
                    parseResult.GetValue(catalogInputOption),
                    parseResult.GetValue(localPathOption),
                    parseResult.GetValue(platformOption),
                    parseResult.GetValue(scopeOption),
                    parseResult.GetValue(dryRunOption));
            }
            catch (Exception ex)
            {
                return WriteError(ex);
            }
        });

        rootCommand.Options.Add(legacyCatalogPathOption);
        rootCommand.Options.Add(legacyLocalPathOption);
        rootCommand.Options.Add(dryRunOption);
        rootCommand.Options.Add(patternsOption);
        rootCommand.Subcommands.Add(defaultSyncCommand);
        rootCommand.Subcommands.Add(listCommand);
        rootCommand.Subcommands.Add(useCommand);
        rootCommand.Subcommands.Add(syncCommand);

        rootCommand.SetAction(parseResult =>
        {
            try
            {
                var catalogPath = parseResult.GetValue(legacyCatalogPathOption);
                var localPath = parseResult.GetValue(legacyLocalPathOption);
                var dryRun = parseResult.GetValue(dryRunOption);
                var patterns = parseResult.GetValue(patternsOption);

                var resolvedCatalogPath = ResolveLegacyCatalogPath(catalogPath);
                if (resolvedCatalogPath is null)
                {
                    Console.Error.WriteLine("Unable to find legacy source catalog path.");
                    Console.Error.WriteLine("Use --catalog to specify one, or ensure one of these exists:");
                    Console.Error.WriteLine($"- {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Code", "User")}");
                    Console.Error.WriteLine($"- {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".copilot")}");
                    return 1;
                }

                var effectivePatterns = GetEffectivePatterns(patterns);
                var localDir = localPath?.FullName ?? Directory.GetCurrentDirectory();
                var agentsSrc = Path.Combine(resolvedCatalogPath.FullName, "agents");
                var agentsDest = Path.Combine(localDir, ".github", "agents");
                var promptsSrc = Path.Combine(resolvedCatalogPath.FullName, "prompts");
                var promptsDest = Path.Combine(localDir, ".github", "prompts");

                Console.WriteLine("Running legacy direct-folder sync.");
                Console.WriteLine($"Syncing from catalog: {resolvedCatalogPath.FullName}");
                Console.WriteLine($"To local repo: {localDir}");
                Console.WriteLine($"Patterns: {string.Join(", ", effectivePatterns)}");

                SyncFolder(agentsSrc, agentsDest, dryRun, effectivePatterns);
                SyncFolder(promptsSrc, promptsDest, dryRun, effectivePatterns);

                Console.WriteLine("Legacy sync complete.");
                return 0;
            }
            catch (Exception ex)
            {
                return WriteError(ex);
            }
        });

        defaultSyncCommand.Options.Add(defaultDryRunOption);
        defaultSyncCommand.SetAction(parseResult =>
        {
            try
            {
                var dryRun = parseResult.GetValue(defaultDryRunOption);
                var vscodeUserPath = GetVsCodeUserPath();
                var copilotUserPath = GetCopilotUserPath();

                if (!Directory.Exists(vscodeUserPath))
                {
                    Console.Error.WriteLine($"VS Code user folder not found: {vscodeUserPath}");
                    return 1;
                }

                Directory.CreateDirectory(copilotUserPath);

                var vscodePrompts = Path.Combine(vscodeUserPath, "prompts");
                var copilotAgents = Path.Combine(copilotUserPath, "agents");

                Console.WriteLine($"Syncing agents from: {vscodePrompts}");
                Console.WriteLine($"To: {copilotAgents}");

                SyncFolder(vscodePrompts, copilotAgents, dryRun, ["*.agent.md"]);

                Console.WriteLine("Default sync complete.");
                return 0;
            }
            catch (Exception ex)
            {
                return WriteError(ex);
            }
        });

        return rootCommand.Parse(args).Invoke();
    }

    private static int HandleList(string? catalogInput, DirectoryInfo? localPath, string? platform, string? scope)
    {
        var context = CreateCatalogContext(catalogInput, localPath, platform, scope);
        var state = LoadInstallState(context.StateFilePath);
        var installed = state.Items
            .Select(x => $"{x.Type}:{x.Name}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Console.WriteLine($"Catalog: {context.CatalogFilePath}");
        Console.WriteLine($"Platform: {context.Platform}");
        Console.WriteLine($"Scope: {context.Scope}");
        Console.WriteLine();

        foreach (var group in context.Catalog.EnumerateEntries()
                     .OrderBy(x => x.Type.ToTypeString())
                     .ThenBy(x => x.Entry.Name, StringComparer.OrdinalIgnoreCase)
                     .GroupBy(x => x.Type))
        {
            Console.WriteLine($"## {group.Key.ToPluralDisplayName()}");
            Console.WriteLine("| Name | Description | Installed | Source |");
            Console.WriteLine("| --- | --- | --- | --- |");

            foreach (var item in group)
            {
                var key = $"{item.Type.ToTypeString()}:{item.Entry.Name}";
                var status = installed.Contains(key) ? "yes" : "no";
                Console.WriteLine($"| {item.Entry.Name} | {EscapePipe(item.Entry.Description)} | {status} | {EscapePipe(item.Entry.Source)} |");
            }

            Console.WriteLine();
        }

        return 0;
    }

    private static int HandleUse(
        string name,
        string? catalogInput,
        DirectoryInfo? localPath,
        string? platform,
        string? scope,
        string? type,
        bool dryRun)
    {
        var context = CreateCatalogContext(catalogInput, localPath, platform, scope);
        var state = LoadInstallState(context.StateFilePath);
        var entryRef = ResolveEntryReference(context.Catalog, name, type);
        var installedThisRun = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        InstallEntryRecursive(context, entryRef, dryRun, installedThisRun);

        if (!dryRun)
        {
            state.Upsert(entryRef);
            foreach (var dependencyKey in installedThisRun)
            {
                var dependency = ResolveTypedReference(context.Catalog, dependencyKey);
                state.Upsert(dependency);
            }

            SaveInstallState(context.StateFilePath, state);
        }

        Console.WriteLine();
        Console.WriteLine($"Installed {entryRef.Type.ToTypeString()}:{entryRef.Entry.Name} for platform '{context.Platform}' scope '{context.Scope}'.");
        return 0;
    }

    private static int HandleSync(
        string? catalogInput,
        DirectoryInfo? localPath,
        string? platform,
        string? scope,
        bool dryRun)
    {
        var context = CreateCatalogContext(catalogInput, localPath, platform, scope);
        var state = LoadInstallState(context.StateFilePath);
        if (state.Items.Count == 0)
        {
            Console.WriteLine("No installed assets tracked for this platform and scope.");
            return 0;
        }

        var installedThisRun = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in state.Items
                     .OrderBy(x => x.Type, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var typedReference = $"{item.Type}:{item.Name}";
            try
            {
                var entryRef = ResolveTypedReference(context.Catalog, typedReference);
                InstallEntryRecursive(context, entryRef, dryRun, installedThisRun);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to sync {typedReference}: {ex.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Sync complete for platform '{context.Platform}' scope '{context.Scope}'.");
        return 0;
    }

    private static CatalogContext CreateCatalogContext(string? catalogInput, DirectoryInfo? localPath, string? platform, string? scope)
    {
        var catalogFilePath = ResolveCatalogFilePath(catalogInput);
        var catalogDirectory = Path.GetDirectoryName(catalogFilePath)
                               ?? throw new InvalidOperationException("Unable to resolve catalog directory.");
        var catalogJson = File.ReadAllText(catalogFilePath);
        var catalog = JsonSerializer.Deserialize<CatalogDocument>(catalogJson, JsonOptions)
                      ?? throw new InvalidOperationException("Unable to deserialize catalog.json.");

        var effectivePlatform = string.IsNullOrWhiteSpace(platform) ? "copilot" : platform.Trim();
        var effectiveScope = string.IsNullOrWhiteSpace(scope) ? "repo" : scope.Trim();
        var targetDirectories = catalog.GetTargetDirectories(effectivePlatform, effectiveScope);

        var localRoot = localPath?.FullName ?? Directory.GetCurrentDirectory();
        var stateFilePath = ResolveStateFilePath(effectiveScope, localRoot, effectivePlatform);

        return new CatalogContext(
            catalogFilePath,
            catalogDirectory,
            catalog,
            effectivePlatform,
            effectiveScope,
            localRoot,
            targetDirectories,
            stateFilePath);
    }

    private static string ResolveCatalogFilePath(string? catalogInput)
    {
        if (!string.IsNullOrWhiteSpace(catalogInput))
        {
            var expanded = ExpandHomeDirectory(catalogInput.Trim());
            if (Directory.Exists(expanded))
            {
                var filePath = Path.Combine(expanded, "catalog.json");
                if (File.Exists(filePath))
                {
                    return filePath;
                }

                throw new InvalidOperationException($"catalog.json not found in directory: {expanded}");
            }

            if (File.Exists(expanded))
            {
                return expanded;
            }

            throw new InvalidOperationException($"Catalog path does not exist: {expanded}");
        }

        var defaultCandidate = Path.Combine(Directory.GetCurrentDirectory(), "catalog.json");
        if (File.Exists(defaultCandidate))
        {
            return defaultCandidate;
        }

        throw new InvalidOperationException("Unable to find catalog.json. Use --catalog to specify a catalog path.");
    }

    private static string ResolveStateFilePath(string scope, string localRoot, string platform)
    {
        var stateRoot = scope.Equals("repo", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(localRoot, ".agentsync")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentsync");

        return Path.Combine(stateRoot, $"{platform}-{scope}-installed.json");
    }

    private static InstallState LoadInstallState(string stateFilePath)
    {
        if (!File.Exists(stateFilePath))
        {
            return new InstallState();
        }

        var json = File.ReadAllText(stateFilePath);
        return JsonSerializer.Deserialize<InstallState>(json, JsonOptions) ?? new InstallState();
    }

    private static void SaveInstallState(string stateFilePath, InstallState state)
    {
        var directory = Path.GetDirectoryName(stateFilePath)
                        ?? throw new InvalidOperationException("Unable to resolve install state directory.");
        Directory.CreateDirectory(directory);
        File.WriteAllText(stateFilePath, JsonSerializer.Serialize(state, JsonOptions));
    }

    private static CatalogEntryReference ResolveEntryReference(CatalogDocument catalog, string name, string? type)
    {
        AssetType? requestedType = null;
        if (!string.IsNullOrWhiteSpace(type))
        {
            requestedType = ParseAssetType(type);
        }

        var matches = catalog.EnumerateEntries()
            .Where(x => x.Entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                        && (!requestedType.HasValue || x.Type == requestedType.Value))
            .ToList();

        if (matches.Count == 0)
        {
            throw new InvalidOperationException($"Catalog entry not found: {name}");
        }

        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Multiple catalog entries matched '{name}'. Re-run with --type. Matches: {string.Join(", ", matches.Select(x => x.Type.ToTypeString()))}");
        }

        return matches[0];
    }

    private static CatalogEntryReference ResolveTypedReference(CatalogDocument catalog, string typedReference)
    {
        var parts = typedReference.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            throw new InvalidOperationException($"Invalid dependency reference: {typedReference}");
        }

        return ResolveEntryReference(catalog, parts[1], parts[0]);
    }

    private static AssetType ParseAssetType(string type)
    {
        return type.Trim().ToLowerInvariant() switch
        {
            "agent" or "agents" => AssetType.Agent,
            "prompt" or "prompts" => AssetType.Prompt,
            "skill" or "skills" => AssetType.Skill,
            "instruction" or "instructions" => AssetType.Instruction,
            _ => throw new InvalidOperationException($"Unsupported asset type: {type}")
        };
    }

    private static void InstallEntryRecursive(
        CatalogContext context,
        CatalogEntryReference entryRef,
        bool dryRun,
        HashSet<string> installedThisRun)
    {
        var typedKey = $"{entryRef.Type.ToTypeString()}:{entryRef.Entry.Name}";
        if (!installedThisRun.Add(typedKey))
        {
            return;
        }

        foreach (var dependency in entryRef.Entry.Requires ?? [])
        {
            var dependencyRef = ResolveTypedReference(context.Catalog, dependency);
            InstallEntryRecursive(context, dependencyRef, dryRun, installedThisRun);
        }

        InstallSingleEntry(context, entryRef, dryRun);
    }

    private static void InstallSingleEntry(CatalogContext context, CatalogEntryReference entryRef, bool dryRun)
    {
        var sourcePath = ResolveSourcePath(context.CatalogDirectory, entryRef.Entry.Source);
        var targetRoot = ResolveTargetRoot(context, entryRef.Type);

        Directory.CreateDirectory(targetRoot);

        switch (entryRef.Type)
        {
            case AssetType.Skill:
            {
                var sourceDirectory = Directory.Exists(sourcePath)
                    ? sourcePath
                    : Path.GetDirectoryName(sourcePath)
                      ?? throw new InvalidOperationException($"Unable to resolve skill source directory for {entryRef.Entry.Name}.");
                var targetDirectory = Path.Combine(targetRoot, entryRef.Entry.Name);
                Console.WriteLine($"Syncing skill {entryRef.Entry.Name}");
                SyncDirectory(sourceDirectory, targetDirectory, dryRun);
                break;
            }
            case AssetType.Agent:
            case AssetType.Prompt:
            case AssetType.Instruction:
            {
                if (Directory.Exists(sourcePath))
                {
                    throw new InvalidOperationException(
                        $"{entryRef.Type.ToTypeString()} '{entryRef.Entry.Name}' source must point to a file for this version of AgentSync.");
                }

                var targetFilePath = Path.Combine(targetRoot, Path.GetFileName(sourcePath));
                Console.WriteLine($"Syncing {entryRef.Type.ToTypeString()} {entryRef.Entry.Name}");
                CopyFileIfNeeded(sourcePath, targetFilePath, dryRun);
                break;
            }
            default:
                throw new InvalidOperationException($"Unsupported asset type: {entryRef.Type}");
        }
    }

    private static string ResolveSourcePath(string catalogDirectory, string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new InvalidOperationException("Catalog entry source cannot be empty.");
        }

        if (source.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || source.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Remote source URLs are not implemented yet: {source}. For now, use local or relative paths.");
        }

        var expanded = ExpandHomeDirectory(source);
        var resolved = Path.IsPathRooted(expanded) ? expanded : Path.GetFullPath(Path.Combine(catalogDirectory, expanded));

        if (File.Exists(resolved) || Directory.Exists(resolved))
        {
            return resolved;
        }

        throw new InvalidOperationException($"Source path not found: {resolved}");
    }

    private static string ResolveTargetRoot(CatalogContext context, AssetType type)
    {
        var relativeOrAbsolutePath = type switch
        {
            AssetType.Agent => context.TargetDirectories.Agents,
            AssetType.Prompt => context.TargetDirectories.Prompts,
            AssetType.Skill => context.TargetDirectories.Skills,
            AssetType.Instruction => context.TargetDirectories.Instructions,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath))
        {
            throw new InvalidOperationException(
                $"No target path configured for {type.ToTypeString()} in platform '{context.Platform}' scope '{context.Scope}'.");
        }

        var expanded = ExpandHomeDirectory(relativeOrAbsolutePath);
        if (Path.IsPathRooted(expanded))
        {
            return expanded;
        }

        return context.Scope.Equals("repo", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFullPath(Path.Combine(context.LocalRoot, expanded))
            : Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), expanded));
    }

    private static string ExpandHomeDirectory(string path)
    {
        if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, path[2..]);
        }

        if (path.Equals("~", StringComparison.Ordinal))
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        return Environment.ExpandEnvironmentVariables(path);
    }

    private static void SyncDirectory(string sourceDirectory, string targetDirectory, bool dryRun)
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
            CopyFileIfNeeded(pair.Value, targetFile, dryRun);
        }

        var targetFiles = Directory.EnumerateFiles(targetDirectory, "*", SearchOption.AllDirectories).ToList();
        foreach (var targetFile in targetFiles)
        {
            var relativePath = Path.GetRelativePath(targetDirectory, targetFile);
            if (sourceFiles.ContainsKey(relativePath))
            {
                continue;
            }

            Console.WriteLine($"Deleting {relativePath}");
            if (!dryRun)
            {
                File.Delete(targetFile);
            }
        }
    }

    private static void CopyFileIfNeeded(string sourceFile, string targetFile, bool dryRun)
    {
        var sourceInfo = new FileInfo(sourceFile);
        var targetInfo = new FileInfo(targetFile);

        var needsCopy = !targetInfo.Exists
                        || sourceInfo.Length != targetInfo.Length
                        || sourceInfo.LastWriteTimeUtc > targetInfo.LastWriteTimeUtc;

        if (!needsCopy)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetFile)
                                  ?? throw new InvalidOperationException($"Unable to resolve target directory for {targetFile}."));

        var relativeDisplay = Path.GetFileName(targetFile);
        Console.WriteLine($"{(targetInfo.Exists ? "Updating" : "Copying")} {relativeDisplay}");

        if (!dryRun)
        {
            File.Copy(sourceFile, targetFile, true);
            File.SetLastWriteTimeUtc(targetFile, sourceInfo.LastWriteTimeUtc);
        }
    }

    private static int WriteError(Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }

    private static void SyncFolder(string src, string dest, bool dryRun, IReadOnlyCollection<string> patterns)
    {
        if (!Directory.Exists(src))
        {
            Console.WriteLine($"Source folder not found: {src}");
            return;
        }

        Directory.CreateDirectory(dest);

        foreach (var srcFile in EnumerateMatchingFiles(src, patterns))
        {
            var relPath = Path.GetRelativePath(src, srcFile);
            var destFile = Path.Combine(dest, relPath);

            Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);

            bool exists = File.Exists(destFile);
            bool needsUpdate = !exists || File.GetLastWriteTimeUtc(srcFile) > File.GetLastWriteTimeUtc(destFile);

            if (needsUpdate)
            {
                var action = exists ? "Updating" : "Copying";
                Console.WriteLine($"{action} {relPath}");
                if (!dryRun)
                {
                    File.Copy(srcFile, destFile, true);
                }
            }
        }

        if (!dryRun)
        {
            foreach (var destFile in EnumerateMatchingFiles(dest, patterns))
            {
                var relPath = Path.GetRelativePath(dest, destFile);
                var srcFile = Path.Combine(src, relPath);
                if (!File.Exists(srcFile))
                {
                    Console.WriteLine($"Deleting {relPath}");
                    File.Delete(destFile);
                }
            }
        }
    }

    private static IReadOnlyCollection<string> GetEffectivePatterns(string[]? rawPatterns)
    {
        if (rawPatterns is null || rawPatterns.Length == 0)
        {
            return DefaultPatterns;
        }

        var patterns = rawPatterns
            .SelectMany(x => x.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return patterns.Length == 0 ? DefaultPatterns : patterns;
    }

    private static IEnumerable<string> EnumerateMatchingFiles(string root, IReadOnlyCollection<string> patterns)
    {
        return patterns
            .SelectMany(pattern => Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static DirectoryInfo? ResolveLegacyCatalogPath(DirectoryInfo? catalogPath)
    {
        if (catalogPath is not null)
        {
            return catalogPath.Exists ? catalogPath : null;
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            Path.Combine(appData, "Code", "User"),
            Path.Combine(userProfile, ".copilot"),
            Path.Combine(appData, "GitHub Copilot", "User")
        };

        foreach (var candidate in candidates)
        {
            if (!Directory.Exists(candidate))
            {
                continue;
            }

            var hasAgents = Directory.Exists(Path.Combine(candidate, "agents"));
            var hasPrompts = Directory.Exists(Path.Combine(candidate, "prompts"));
            if (hasAgents || hasPrompts)
            {
                return new DirectoryInfo(candidate);
            }
        }

        return null;
    }

    private static string GetVsCodeUserPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Code", "User");
    }

    private static string GetCopilotUserPath()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var homeCopilot = Path.Combine(userProfile, ".copilot");
        if (Directory.Exists(homeCopilot))
        {
            return homeCopilot;
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "GitHub Copilot", "User");
    }

    private static string EscapePipe(string? value) => (value ?? string.Empty).Replace("|", "\\|");
}

internal sealed record CatalogContext(
    string CatalogFilePath,
    string CatalogDirectory,
    CatalogDocument Catalog,
    string Platform,
    string Scope,
    string LocalRoot,
    TargetDirectories TargetDirectories,
    string StateFilePath);

internal sealed class CatalogDocument
{
    [JsonPropertyName("version")]
    public int Version { get; init; } = 1;

    [JsonPropertyName("targets")]
    public Dictionary<string, Dictionary<string, TargetDirectories>> Targets { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("catalog")]
    public CatalogCollections Catalog { get; init; } = new();

    public IEnumerable<CatalogEntryReference> EnumerateEntries()
    {
        foreach (var entry in Catalog.Agents)
        {
            yield return new CatalogEntryReference(AssetType.Agent, entry);
        }

        foreach (var entry in Catalog.Prompts)
        {
            yield return new CatalogEntryReference(AssetType.Prompt, entry);
        }

        foreach (var entry in Catalog.Skills)
        {
            yield return new CatalogEntryReference(AssetType.Skill, entry);
        }

        foreach (var entry in Catalog.Instructions)
        {
            yield return new CatalogEntryReference(AssetType.Instruction, entry);
        }
    }

    public TargetDirectories GetTargetDirectories(string platform, string scope)
    {
        if (!Targets.TryGetValue(platform, out var scopes))
        {
            throw new InvalidOperationException($"Platform '{platform}' was not found in catalog targets.");
        }

        if (!scopes.TryGetValue(scope, out var directories))
        {
            throw new InvalidOperationException($"Scope '{scope}' was not found for platform '{platform}' in catalog targets.");
        }

        return directories;
    }
}

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

internal sealed record CatalogEntryReference(AssetType Type, CatalogEntry Entry);

internal sealed class InstallState
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("items")]
    public List<InstalledCatalogItem> Items { get; set; } = [];

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
}

internal sealed class InstalledCatalogItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

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
