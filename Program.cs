using System.CommandLine;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

internal enum AssetType
{
    Agent,
    Prompt,
    Skill,
    Instruction
}

internal enum GitHubContentType
{
    File,
    Directory
}

internal sealed class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    private static readonly HttpClient GitHubClient = CreateGitHubClient();

    private static int Main(string[] args)
    {
        var rootCommand = new RootCommand("Manage catalog-driven Copilot assets.");

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

        var dryRunOption = new Option<bool>("--dry-run", "-d")
        {
            Description = "Show what would happen without making changes."
        };

        var forceOption = new Option<bool>("--force", "-f")
        {
            Description = "Allow AgentSync to overwrite existing unmanaged content."
        };

        var listCommand = new Command("list", "List catalog entries and their install status.");
        listCommand.Options.Add(catalogInputOption);
        listCommand.Options.Add(localPathOption);
        listCommand.Options.Add(platformOption);
        listCommand.Options.Add(scopeOption);
        listCommand.SetAction(parseResult => Execute(() =>
            HandleList(
                parseResult.GetValue(catalogInputOption),
                parseResult.GetValue(localPathOption),
                parseResult.GetValue(platformOption),
                parseResult.GetValue(scopeOption))));

        var searchCommand = new Command("search", "Search catalog entries by name, description, and tags.");
        var searchQueryArgument = new Argument<string?>("query")
        {
            Description = "Search text. If omitted, all catalog entries are shown.",
            Arity = ArgumentArity.ZeroOrOne
        };
        searchCommand.Arguments.Add(searchQueryArgument);
        searchCommand.Options.Add(catalogInputOption);
        searchCommand.Options.Add(localPathOption);
        searchCommand.Options.Add(platformOption);
        searchCommand.Options.Add(scopeOption);
        searchCommand.SetAction(parseResult => Execute(() =>
            HandleSearch(
                parseResult.GetValue(searchQueryArgument),
                parseResult.GetValue(catalogInputOption),
                parseResult.GetValue(localPathOption),
                parseResult.GetValue(platformOption),
                parseResult.GetValue(scopeOption))));

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
        useCommand.Options.Add(forceOption);
        useCommand.SetAction(parseResult => Execute(() =>
            HandleUse(
                parseResult.GetValue(useNameArgument)!,
                parseResult.GetValue(catalogInputOption),
                parseResult.GetValue(localPathOption),
                parseResult.GetValue(platformOption),
                parseResult.GetValue(scopeOption),
                parseResult.GetValue(typeOption),
                parseResult.GetValue(dryRunOption),
                parseResult.GetValue(forceOption))));

        var installCommand = new Command("install", "Install all catalog assets or the named subset into the selected target.");
        var installNamesArgument = new Argument<string[]>("names")
        {
            Description = "Optional catalog entry names to install. If omitted, every entry is installed.",
            Arity = ArgumentArity.ZeroOrMore
        };
        installCommand.Arguments.Add(installNamesArgument);
        installCommand.Options.Add(catalogInputOption);
        installCommand.Options.Add(localPathOption);
        installCommand.Options.Add(platformOption);
        installCommand.Options.Add(scopeOption);
        installCommand.Options.Add(dryRunOption);
        installCommand.Options.Add(forceOption);
        installCommand.SetAction(parseResult => Execute(() =>
            HandleInstall(
                parseResult.GetValue(installNamesArgument) ?? [],
                parseResult.GetValue(catalogInputOption),
                parseResult.GetValue(localPathOption),
                parseResult.GetValue(platformOption),
                parseResult.GetValue(scopeOption),
                parseResult.GetValue(dryRunOption),
                parseResult.GetValue(forceOption))));

        var syncCommand = new Command("sync", "Refresh every asset previously installed by AgentSync for the selected platform and scope.");
        syncCommand.Options.Add(catalogInputOption);
        syncCommand.Options.Add(localPathOption);
        syncCommand.Options.Add(platformOption);
        syncCommand.Options.Add(scopeOption);
        syncCommand.Options.Add(dryRunOption);
        syncCommand.Options.Add(forceOption);
        syncCommand.SetAction(parseResult => Execute(() =>
            HandleSync(
                parseResult.GetValue(catalogInputOption),
                parseResult.GetValue(localPathOption),
                parseResult.GetValue(platformOption),
                parseResult.GetValue(scopeOption),
                parseResult.GetValue(dryRunOption),
                parseResult.GetValue(forceOption))));

        var importCommand = new Command("import", "Discover unmanaged assets and bring matching catalog entries under management.");
        importCommand.Aliases.Add("migrate");
        var importSourceOption = new Option<string?>("--source", "-s")
        {
            Description = "Known source alias or path to scan. Supported aliases: auto, vscode, copilot, repo.",
            Required = false
        };
        importCommand.Options.Add(catalogInputOption);
        importCommand.Options.Add(localPathOption);
        importCommand.Options.Add(platformOption);
        importCommand.Options.Add(scopeOption);
        importCommand.Options.Add(importSourceOption);
        importCommand.Options.Add(dryRunOption);
        importCommand.Options.Add(forceOption);
        importCommand.SetAction(parseResult => Execute(() =>
            HandleImport(
                parseResult.GetValue(catalogInputOption),
                parseResult.GetValue(localPathOption),
                parseResult.GetValue(platformOption),
                parseResult.GetValue(scopeOption),
                parseResult.GetValue(importSourceOption),
                parseResult.GetValue(dryRunOption),
                parseResult.GetValue(forceOption))));

        var removeCommand = new Command("remove", "Uninstall a managed asset and remove its tracked install state.");
        var removeNameArgument = new Argument<string>("name")
        {
            Description = "Catalog entry name."
        };
        removeCommand.Arguments.Add(removeNameArgument);
        removeCommand.Options.Add(catalogInputOption);
        removeCommand.Options.Add(localPathOption);
        removeCommand.Options.Add(platformOption);
        removeCommand.Options.Add(scopeOption);
        removeCommand.Options.Add(typeOption);
        removeCommand.Options.Add(dryRunOption);
        removeCommand.SetAction(parseResult => Execute(() =>
            HandleRemove(
                parseResult.GetValue(removeNameArgument)!,
                parseResult.GetValue(catalogInputOption),
                parseResult.GetValue(localPathOption),
                parseResult.GetValue(platformOption),
                parseResult.GetValue(scopeOption),
                parseResult.GetValue(typeOption),
                parseResult.GetValue(dryRunOption))));

        var addCommand = new Command("add", "Register a new asset in catalog.json.");
        var addNameArgument = new Argument<string>("name")
        {
            Description = "Catalog entry name."
        };
        var addTypeOption = new Option<string>("--type", "-t")
        {
            Description = "Asset type: agent, prompt, skill, or instruction."
        };
        var addSourceOption = new Option<string>("--source", "-s")
        {
            Description = "Local filesystem path or supported GitHub URL for the source."
        };
        var addDescriptionOption = new Option<string?>("--description")
        {
            Description = "Optional short description stored in catalog.json."
        };
        var addTagsOption = new Option<string[]>("--tag")
        {
            Description = "Optional tag. Repeat --tag for multiple values."
        };
        var addRequiresOption = new Option<string[]>("--require")
        {
            Description = "Typed dependency reference such as skill:common. Repeat --require for multiple values."
        };
        addCommand.Arguments.Add(addNameArgument);
        addCommand.Options.Add(catalogInputOption);
        addCommand.Options.Add(addTypeOption);
        addCommand.Options.Add(addSourceOption);
        addCommand.Options.Add(addDescriptionOption);
        addCommand.Options.Add(addTagsOption);
        addCommand.Options.Add(addRequiresOption);
        addCommand.SetAction(parseResult => Execute(() =>
            HandleAdd(
                parseResult.GetValue(addNameArgument)!,
                parseResult.GetValue(catalogInputOption),
                parseResult.GetValue(addTypeOption),
                parseResult.GetValue(addSourceOption),
                parseResult.GetValue(addDescriptionOption),
                parseResult.GetValue(addTagsOption) ?? [],
                parseResult.GetValue(addRequiresOption) ?? [])));

        var pushCommand = new Command("push", "Push local managed changes back to the configured source.");
        var pushNameArgument = new Argument<string>("name")
        {
            Description = "Catalog entry name."
        };
        var pushMessageOption = new Option<string?>("--message", "-m")
        {
            Description = "Optional commit message used for GitHub-backed sources.",
            Required = false
        };
        pushCommand.Arguments.Add(pushNameArgument);
        pushCommand.Options.Add(catalogInputOption);
        pushCommand.Options.Add(localPathOption);
        pushCommand.Options.Add(platformOption);
        pushCommand.Options.Add(scopeOption);
        pushCommand.Options.Add(typeOption);
        pushCommand.Options.Add(dryRunOption);
        pushCommand.Options.Add(pushMessageOption);
        pushCommand.SetAction(parseResult => Execute(() =>
            HandlePush(
                parseResult.GetValue(pushNameArgument)!,
                parseResult.GetValue(catalogInputOption),
                parseResult.GetValue(localPathOption),
                parseResult.GetValue(platformOption),
                parseResult.GetValue(scopeOption),
                parseResult.GetValue(typeOption),
                parseResult.GetValue(dryRunOption),
                parseResult.GetValue(pushMessageOption))));

        rootCommand.Subcommands.Add(listCommand);
        rootCommand.Subcommands.Add(searchCommand);
        rootCommand.Subcommands.Add(useCommand);
        rootCommand.Subcommands.Add(installCommand);
        rootCommand.Subcommands.Add(syncCommand);
        rootCommand.Subcommands.Add(importCommand);
        rootCommand.Subcommands.Add(removeCommand);
        rootCommand.Subcommands.Add(addCommand);
        rootCommand.Subcommands.Add(pushCommand);

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
            Console.WriteLine("| Name | Description | Installed | Source | Tags |");
            Console.WriteLine("| --- | --- | --- | --- | --- |");

            foreach (var item in group)
            {
                var key = $"{item.Type.ToTypeString()}:{item.Entry.Name}";
                var status = installed.Contains(key) ? "yes" : "no";
                var tags = item.Entry.Tags is { Count: > 0 }
                    ? string.Join(", ", item.Entry.Tags)
                    : string.Empty;
                Console.WriteLine(
                    $"| {EscapePipe(item.Entry.Name)} | {EscapePipe(item.Entry.Description)} | {status} | {EscapePipe(item.Entry.Source)} | {EscapePipe(tags)} |");
            }

            Console.WriteLine();
        }

        return 0;
    }

    private static int HandleSearch(
        string? query,
        string? catalogInput,
        DirectoryInfo? localPath,
        string? platform,
        string? scope)
    {
        var context = CreateCatalogContext(catalogInput, localPath, platform, scope);
        var filtered = FilterEntries(context.Catalog.EnumerateEntries(), query);

        Console.WriteLine($"Catalog: {context.CatalogFilePath}");
        Console.WriteLine($"Search: {(string.IsNullOrWhiteSpace(query) ? "(all entries)" : query.Trim())}");
        Console.WriteLine();

        if (filtered.Count == 0)
        {
            Console.WriteLine("No catalog entries matched.");
            return 0;
        }

        foreach (var group in filtered
                     .OrderBy(x => x.Type.ToTypeString())
                     .ThenBy(x => x.Entry.Name, StringComparer.OrdinalIgnoreCase)
                     .GroupBy(x => x.Type))
        {
            Console.WriteLine($"## {group.Key.ToPluralDisplayName()}");
            foreach (var item in group)
            {
                var tags = item.Entry.Tags is { Count: > 0 }
                    ? $" [{string.Join(", ", item.Entry.Tags)}]"
                    : string.Empty;
                Console.WriteLine($"- {item.Entry.Name}{tags}");
                if (!string.IsNullOrWhiteSpace(item.Entry.Description))
                {
                    Console.WriteLine($"  {item.Entry.Description}");
                }
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
        bool dryRun,
        bool force)
    {
        var context = CreateCatalogContext(catalogInput, localPath, platform, scope);
        var state = LoadInstallState(context.StateFilePath);
        var entryRef = ResolveEntryReference(context.Catalog, name, type);
        var installedThisRun = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        InstallEntryRecursive(context, state, entryRef, new ExecutionOptions(dryRun, force), installedThisRun);

        PersistInstalledEntries(context, state, installedThisRun, dryRun);

        Console.WriteLine();
        Console.WriteLine($"Installed {entryRef.Type.ToTypeString()}:{entryRef.Entry.Name} for platform '{context.Platform}' scope '{context.Scope}'.");
        return 0;
    }

    private static int HandleInstall(
        IReadOnlyCollection<string> names,
        string? catalogInput,
        DirectoryInfo? localPath,
        string? platform,
        string? scope,
        bool dryRun,
        bool force)
    {
        var context = CreateCatalogContext(catalogInput, localPath, platform, scope);
        var state = LoadInstallState(context.StateFilePath);
        var installedThisRun = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var entries = names.Count == 0
            ? context.Catalog.EnumerateEntries()
                .OrderBy(x => x.Type.ToTypeString())
                .ThenBy(x => x.Entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : names
                .Select(name => ResolveEntryReference(context.Catalog, name, null))
                .DistinctBy(x => $"{x.Type.ToTypeString()}:{x.Entry.Name}", StringComparer.OrdinalIgnoreCase)
                .ToList();

        foreach (var entryRef in entries)
        {
            InstallEntryRecursive(context, state, entryRef, new ExecutionOptions(dryRun, force), installedThisRun);
        }

        PersistInstalledEntries(context, state, installedThisRun, dryRun);

        Console.WriteLine();
        Console.WriteLine($"Installed {entries.Count} catalog asset(s) for platform '{context.Platform}' scope '{context.Scope}'.");
        return 0;
    }

    private static int HandleSync(
        string? catalogInput,
        DirectoryInfo? localPath,
        string? platform,
        string? scope,
        bool dryRun,
        bool force)
    {
        var context = CreateCatalogContext(catalogInput, localPath, platform, scope);
        var state = LoadInstallState(context.StateFilePath);
        if (state.Items.Count == 0)
        {
            Console.WriteLine("No installed assets tracked for this platform and scope.");
            return 0;
        }

        var installedThisRun = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var failures = new List<string>();

        foreach (var item in state.Items
                     .OrderBy(x => x.Type, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var typedReference = $"{item.Type}:{item.Name}";
            try
            {
                var entryRef = ResolveTypedReference(context.Catalog, typedReference);
                InstallEntryRecursive(context, state, entryRef, new ExecutionOptions(dryRun, force), installedThisRun);
            }
            catch (Exception ex)
            {
                var message = $"Failed to sync {typedReference}: {ex.Message}";
                failures.Add(message);
                Console.Error.WriteLine(message);
            }
        }

        PersistInstalledEntries(context, state, installedThisRun, dryRun);

        Console.WriteLine();
        Console.WriteLine($"Sync complete for platform '{context.Platform}' scope '{context.Scope}'.");
        return failures.Count == 0 ? 0 : 1;
    }

    private static int HandleImport(
        string? catalogInput,
        DirectoryInfo? localPath,
        string? platform,
        string? scope,
        string? source,
        bool dryRun,
        bool force)
    {
        var context = CreateCatalogContext(catalogInput, localPath, platform, scope);
        var state = LoadInstallState(context.StateFilePath);
        var discovered = DiscoverUnmanagedAssets(context, source);
        if (discovered.Count == 0)
        {
            Console.WriteLine("No unmanaged assets were discovered.");
            return 0;
        }

        var report = new MigrationReport();
        var installedThisRun = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var failures = new List<string>();

        foreach (var item in discovered
                     .OrderBy(x => x.Type.ToTypeString())
                     .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase))
        {
            if (state.Contains(item.Type, item.Name) || installedThisRun.Contains($"{item.Type.ToTypeString()}:{item.Name}"))
            {
                report.Skipped.Add(new MigrationReportItem(item, "Already managed."));
                continue;
            }

            var mappedEntry = TryMapDiscoveredAsset(context, item);
            if (mappedEntry is null)
            {
                report.Unmapped.Add(new MigrationReportItem(item, "No unique catalog entry matched the discovered asset."));
                continue;
            }

            try
            {
                var importedForItem = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                InstallEntryRecursive(context, state, mappedEntry, new ExecutionOptions(dryRun, force), importedForItem);
                foreach (var key in importedForItem)
                {
                    installedThisRun.Add(key);
                }

                report.Imported.Add(new MigrationReportItem(item, $"Mapped to {mappedEntry.Type.ToTypeString()}:{mappedEntry.Entry.Name}."));
            }
            catch (Exception ex)
            {
                failures.Add(ex.Message);
                report.Skipped.Add(new MigrationReportItem(item, ex.Message));
            }
        }

        PersistInstalledEntries(context, state, installedThisRun, dryRun);
        PrintMigrationReport(report, dryRun);
        return failures.Count == 0 ? 0 : 1;
    }

    private static int HandleRemove(
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
        var targetPath = ResolveInstalledTargetPath(context, entryRef);

        if (!File.Exists(targetPath) && !Directory.Exists(targetPath) && !state.Contains(entryRef.Type, entryRef.Entry.Name))
        {
            Console.WriteLine($"Nothing to remove for {entryRef.Type.ToTypeString()}:{entryRef.Entry.Name}.");
            return 0;
        }

        RemovePath(targetPath, dryRun);

        if (!dryRun)
        {
            state.Remove(entryRef.Type, entryRef.Entry.Name);
            SaveInstallState(context.StateFilePath, state);
        }

        Console.WriteLine($"Removed {entryRef.Type.ToTypeString()}:{entryRef.Entry.Name}.");
        return 0;
    }

    private static int HandleAdd(
        string name,
        string? catalogInput,
        string? type,
        string? source,
        string? description,
        IReadOnlyCollection<string> tags,
        IReadOnlyCollection<string> requires)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new InvalidOperationException("The --type option is required for add.");
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new InvalidOperationException("The --source option is required for add.");
        }

        var catalogFilePath = ResolveCatalogFilePath(catalogInput);
        var catalogDirectory = Path.GetDirectoryName(catalogFilePath)
                               ?? throw new InvalidOperationException("Unable to resolve catalog directory.");
        var catalog = LoadCatalogDocument(catalogFilePath);
        var assetType = ParseAssetType(type);

        if (catalog.EnumerateEntries().Any(x =>
                x.Type == assetType && x.Entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Catalog entry already exists: {assetType.ToTypeString()}:{name}");
        }

        var normalizedRequires = requires
            .SelectMany(SplitOptionValues)
            .ToList();
        foreach (var dependency in normalizedRequires)
        {
            ValidateTypedReference(dependency);
        }

        var entry = new CatalogEntry
        {
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty,
            Source = NormalizeCatalogSourceForStorage(catalogDirectory, source),
            Requires = normalizedRequires.Count == 0 ? null : normalizedRequires,
            Tags = tags.SelectMany(SplitOptionValues).ToList() is { Count: > 0 } normalizedTags ? normalizedTags : null
        };

        catalog.AddEntry(assetType, entry);
        ValidateCatalogDocument(catalog, catalogFilePath);
        SaveCatalogDocument(catalogFilePath, catalog);

        Console.WriteLine($"Added {assetType.ToTypeString()}:{entry.Name} to {catalogFilePath}.");
        return 0;
    }

    private static int HandlePush(
        string name,
        string? catalogInput,
        DirectoryInfo? localPath,
        string? platform,
        string? scope,
        string? type,
        bool dryRun,
        string? message)
    {
        var context = CreateCatalogContext(catalogInput, localPath, platform, scope);
        var entryRef = ResolveEntryReference(context.Catalog, name, type);
        var localInstalledPath = ResolveInstalledTargetPath(context, entryRef);

        if (!File.Exists(localInstalledPath) && !Directory.Exists(localInstalledPath))
        {
            throw new InvalidOperationException(
                $"Managed content was not found at '{localInstalledPath}'. Install or sync the asset before pushing.");
        }

        if (TryParseGitHubSource(entryRef.Entry.Source, out var gitHubSource))
        {
            PushToGitHub(entryRef, gitHubSource, localInstalledPath, dryRun, message);
        }
        else
        {
            var sourcePath = ResolveLocalSourcePath(context.CatalogDirectory, entryRef.Entry.Source);
            PushToLocalSource(entryRef, sourcePath, localInstalledPath, dryRun);
        }

        Console.WriteLine($"Pushed {entryRef.Type.ToTypeString()}:{entryRef.Entry.Name}.");
        return 0;
    }

    private static CatalogContext CreateCatalogContext(
        string? catalogInput,
        DirectoryInfo? localPath,
        string? platform,
        string? scope)
    {
        var catalogFilePath = ResolveCatalogFilePath(catalogInput);
        var catalogDirectory = Path.GetDirectoryName(catalogFilePath)
                               ?? throw new InvalidOperationException("Unable to resolve catalog directory.");
        var catalog = LoadCatalogDocument(catalogFilePath);

        var effectivePlatform = string.IsNullOrWhiteSpace(platform) ? "copilot" : platform.Trim();
        var effectiveScope = string.IsNullOrWhiteSpace(scope) ? "repo" : scope.Trim();
        var targetDirectories = catalog.GetTargetDirectories(effectivePlatform, effectiveScope);

        var localRoot = localPath?.FullName ?? Directory.GetCurrentDirectory();
        if (effectiveScope.Equals("repo", StringComparison.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(localRoot);
        }

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

    private static CatalogDocument LoadCatalogDocument(string catalogFilePath)
    {
        var catalogJson = File.ReadAllText(catalogFilePath);
        var catalog = JsonSerializer.Deserialize<CatalogDocument>(catalogJson, JsonOptions)
                      ?? throw new InvalidOperationException("Unable to deserialize catalog.json.");
        ValidateCatalogDocument(catalog, catalogFilePath);
        return catalog;
    }

    private static void SaveCatalogDocument(string catalogFilePath, CatalogDocument catalog)
    {
        File.WriteAllText(catalogFilePath, JsonSerializer.Serialize(catalog, JsonOptions));
    }

    private static string ResolveCatalogFilePath(string? catalogInput)
    {
        if (!string.IsNullOrWhiteSpace(catalogInput))
        {
            var expanded = ExpandConfiguredPath(catalogInput.Trim());
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

    private static void ValidateCatalogDocument(CatalogDocument catalog, string catalogFilePath)
    {
        if (catalog.Version <= 0)
        {
            throw new InvalidOperationException($"catalog.json has an invalid version in {catalogFilePath}.");
        }

        if (catalog.Targets.Count == 0)
        {
            throw new InvalidOperationException($"catalog.json in {catalogFilePath} must define at least one platform in targets.");
        }

        foreach (var platform in catalog.Targets)
        {
            if (platform.Value.Count == 0)
            {
                throw new InvalidOperationException($"Platform '{platform.Key}' in {catalogFilePath} must define at least one scope.");
            }
        }

        var seenEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entryRef in catalog.EnumerateEntries())
        {
            if (string.IsNullOrWhiteSpace(entryRef.Entry.Name))
            {
                throw new InvalidOperationException($"{entryRef.Type.ToTypeString()} entries in {catalogFilePath} must have a non-empty name.");
            }

            if (string.IsNullOrWhiteSpace(entryRef.Entry.Source))
            {
                throw new InvalidOperationException($"{entryRef.Type.ToTypeString()} '{entryRef.Entry.Name}' must have a non-empty source.");
            }

            var typedKey = $"{entryRef.Type.ToTypeString()}:{entryRef.Entry.Name}";
            if (!seenEntries.Add(typedKey))
            {
                throw new InvalidOperationException($"catalog.json contains a duplicate entry: {typedKey}");
            }

            foreach (var dependency in entryRef.Entry.Requires ?? [])
            {
                ValidateTypedReference(dependency);
            }
        }
    }

    private static void ValidateTypedReference(string dependency)
    {
        var parts = dependency.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            throw new InvalidOperationException($"Invalid typed dependency reference: {dependency}");
        }

        _ = ParseAssetType(parts[0]);
    }

    private static string ResolveStateFilePath(string scope, string localRoot, string platform)
    {
        var stateRoot = scope.Equals("repo", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(localRoot, ".agentsync")
            : Path.Combine(GetCopilotHomePath(), ".agentsync-state");

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

    private static List<CatalogEntryReference> FilterEntries(IEnumerable<CatalogEntryReference> entries, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return entries.ToList();
        }

        var parts = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return entries
            .Where(entry =>
                parts.All(part =>
                    entry.Entry.Name.Contains(part, StringComparison.OrdinalIgnoreCase)
                    || entry.Entry.Description.Contains(part, StringComparison.OrdinalIgnoreCase)
                    || (entry.Entry.Tags?.Any(tag => tag.Contains(part, StringComparison.OrdinalIgnoreCase)) ?? false)))
            .ToList();
    }

    private static void InstallEntryRecursive(
        CatalogContext context,
        InstallState state,
        CatalogEntryReference entryRef,
        ExecutionOptions options,
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
            InstallEntryRecursive(context, state, dependencyRef, options, installedThisRun);
        }

        InstallSingleEntry(context, state, entryRef, options);
    }

    private static void InstallSingleEntry(
        CatalogContext context,
        InstallState state,
        CatalogEntryReference entryRef,
        ExecutionOptions options)
    {
        var targetPath = ResolveInstalledTargetPath(context, entryRef);

        using var resolvedSource = ResolveEntrySource(context, entryRef);
        if (entryRef.Type == AssetType.Skill)
        {
            if (!Directory.Exists(resolvedSource.LocalPath))
            {
                throw new InvalidOperationException($"Skill source was expected to resolve to a directory: {resolvedSource.LocalPath}");
            }

            EnsureDirectoryInstallAllowed(targetPath, state, entryRef, options.Force);
            Console.WriteLine($"{(Directory.Exists(targetPath) ? ActionVerb("sync", options.DryRun) : ActionVerb("install", options.DryRun))} skill {entryRef.Entry.Name}");
            SyncDirectory(resolvedSource.LocalPath, targetPath, options.DryRun);
            return;
        }

        if (Directory.Exists(resolvedSource.LocalPath))
        {
            throw new InvalidOperationException(
                $"{entryRef.Type.ToTypeString()} '{entryRef.Entry.Name}' source must point to a file.");
        }

        EnsureFileInstallAllowed(resolvedSource.LocalPath, targetPath, state, entryRef, options.Force);
        Console.WriteLine($"{ActionVerb(File.Exists(targetPath) ? "update" : "install", options.DryRun)} {entryRef.Type.ToTypeString()} {entryRef.Entry.Name}");
        CopyFileIfNeeded(resolvedSource.LocalPath, targetPath, options.DryRun, overwriteExisting: true);
    }

    private static void PersistInstalledEntries(
        CatalogContext context,
        InstallState state,
        IEnumerable<string> installedEntries,
        bool dryRun)
    {
        if (dryRun)
        {
            return;
        }

        foreach (var typedReference in installedEntries)
        {
            var entryRef = ResolveTypedReference(context.Catalog, typedReference);
            state.Upsert(entryRef);
        }

        SaveInstallState(context.StateFilePath, state);
    }

    private static ResolvedAssetSource ResolveEntrySource(CatalogContext context, CatalogEntryReference entryRef)
    {
        if (TryParseGitHubSource(entryRef.Entry.Source, out var gitHubSource))
        {
            return DownloadGitHubSource(entryRef, gitHubSource);
        }

        var sourcePath = ResolveLocalSourcePath(context.CatalogDirectory, entryRef.Entry.Source);
        return new ResolvedAssetSource(sourcePath, false);
    }

    private static string ResolveLocalSourcePath(string catalogDirectory, string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new InvalidOperationException("Catalog entry source cannot be empty.");
        }

        var expanded = ExpandConfiguredPath(source);
        var resolved = Path.IsPathRooted(expanded) ? expanded : Path.GetFullPath(Path.Combine(catalogDirectory, expanded));

        if (File.Exists(resolved) || Directory.Exists(resolved))
        {
            return resolved;
        }

        throw new InvalidOperationException($"Source path not found: {resolved}");
    }

    private static string ResolveInstalledTargetPath(CatalogContext context, CatalogEntryReference entryRef)
    {
        var targetRoot = ResolveTargetRoot(context, entryRef.Type);
        return entryRef.Type switch
        {
            AssetType.Skill => Path.Combine(targetRoot, entryRef.Entry.Name),
            AssetType.Agent or AssetType.Prompt or AssetType.Instruction => Path.Combine(targetRoot, ResolveAssetFileName(entryRef)),
            _ => throw new InvalidOperationException($"Unsupported asset type: {entryRef.Type}")
        };
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

        var expanded = ExpandConfiguredPath(relativeOrAbsolutePath);
        if (Path.IsPathRooted(expanded))
        {
            return expanded;
        }

        if (context.Scope.Equals("repo", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFullPath(Path.Combine(context.LocalRoot, expanded));
        }

        return Path.GetFullPath(Path.Combine(GetUserHomePath(), expanded));
    }

    private static string ResolveAssetFileName(CatalogEntryReference entryRef)
    {
        if (TryParseGitHubSource(entryRef.Entry.Source, out var gitHubSource))
        {
            var fileName = Path.GetFileName(gitHubSource.Path);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return fileName;
            }
        }

        var expanded = ExpandConfiguredPath(entryRef.Entry.Source);
        var fileNameFromSource = Path.GetFileName(expanded);
        if (!string.IsNullOrWhiteSpace(fileNameFromSource))
        {
            return fileNameFromSource;
        }

        return entryRef.Type switch
        {
            AssetType.Agent => $"{entryRef.Entry.Name}.agent.md",
            AssetType.Prompt => $"{entryRef.Entry.Name}.prompt.md",
            AssetType.Instruction => $"{entryRef.Entry.Name}.instructions.md",
            _ => throw new InvalidOperationException($"Unsupported file-based asset type: {entryRef.Type}")
        };
    }

    private static void EnsureFileInstallAllowed(
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

    private static void EnsureDirectoryInstallAllowed(
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
            CopyFileIfNeeded(pair.Value, targetFile, dryRun, overwriteExisting: true);
        }

        var targetFiles = Directory.EnumerateFiles(targetDirectory, "*", SearchOption.AllDirectories).ToList();
        foreach (var targetFile in targetFiles)
        {
            var relativePath = Path.GetRelativePath(targetDirectory, targetFile);
            if (sourceFiles.ContainsKey(relativePath))
            {
                continue;
            }

            Console.WriteLine($"{ActionVerb("delete", dryRun)} {relativePath}");
            if (!dryRun)
            {
                File.Delete(targetFile);
            }
        }
    }

    private static void CopyFileIfNeeded(string sourceFile, string targetFile, bool dryRun, bool overwriteExisting)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetFile)
                                  ?? throw new InvalidOperationException($"Unable to resolve target directory for {targetFile}."));

        var action = File.Exists(targetFile)
            ? (FilesAreEqual(sourceFile, targetFile) ? "skip" : "update")
            : "copy";

        if (action == "skip")
        {
            Console.WriteLine("Skipping unchanged " + Path.GetRelativePath(Path.GetDirectoryName(targetFile) ?? targetFile, targetFile));
            return;
        }

        Console.WriteLine($"{ActionVerb(action, dryRun)} {targetFile}");
        if (!dryRun)
        {
            File.Copy(sourceFile, targetFile, overwriteExisting);
            File.SetLastWriteTimeUtc(targetFile, File.GetLastWriteTimeUtc(sourceFile));
        }
    }

    private static bool FilesAreEqual(string leftPath, string rightPath)
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

    private static void RemovePath(string path, bool dryRun)
    {
        if (File.Exists(path))
        {
            Console.WriteLine($"{ActionVerb("delete", dryRun)} {path}");
            if (!dryRun)
            {
                File.Delete(path);
            }

            return;
        }

        if (Directory.Exists(path))
        {
            Console.WriteLine($"{ActionVerb("delete", dryRun)} {path}");
            if (!dryRun)
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }

    private static List<DiscoveredAsset> DiscoverUnmanagedAssets(CatalogContext context, string? source)
    {
        var results = new Dictionary<string, DiscoveredAsset>(StringComparer.OrdinalIgnoreCase);
        foreach (var location in EnumerateDiscoveryLocations(context, source))
        {
            if (!Directory.Exists(location.Root))
            {
                continue;
            }

            foreach (var item in DiscoverAssetsInLocation(location))
            {
                results.TryAdd($"{item.Type.ToTypeString()}:{item.Path}", item);
            }
        }

        return results.Values.ToList();
    }

    private static IEnumerable<DiscoveryLocation> EnumerateDiscoveryLocations(CatalogContext context, string? source)
    {
        if (string.IsNullOrWhiteSpace(source) || source.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var item in GetKnownDiscoveryLocations(context))
            {
                yield return item;
            }

            yield break;
        }

        if (source.Equals("vscode", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var item in GetVsCodeDiscoveryLocations())
            {
                yield return item;
            }

            yield break;
        }

        if (source.Equals("copilot", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var item in GetCopilotDiscoveryLocations())
            {
                yield return item;
            }

            yield break;
        }

        if (source.Equals("repo", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var item in GetRepoDiscoveryLocations(context.LocalRoot))
            {
                yield return item;
            }

            yield break;
        }

        var expandedPath = ExpandConfiguredPath(source);
        if (!Directory.Exists(expandedPath))
        {
            throw new InvalidOperationException($"Import source path not found: {expandedPath}");
        }

        yield return new DiscoveryLocation("custom-agents", AssetType.Agent, Path.Combine(expandedPath, "agents"));
        yield return new DiscoveryLocation("custom-prompts", AssetType.Prompt, Path.Combine(expandedPath, "prompts"));
        yield return new DiscoveryLocation("custom-skills", AssetType.Skill, Path.Combine(expandedPath, "skills"));
        yield return new DiscoveryLocation("custom-instructions", AssetType.Instruction, Path.Combine(expandedPath, "instructions"));
    }

    private static IEnumerable<DiscoveryLocation> GetKnownDiscoveryLocations(CatalogContext context)
    {
        foreach (var item in GetVsCodeDiscoveryLocations())
        {
            yield return item;
        }

        foreach (var item in GetCopilotDiscoveryLocations())
        {
            yield return item;
        }

        foreach (var item in GetRepoDiscoveryLocations(context.LocalRoot))
        {
            yield return item;
        }
    }

    private static IEnumerable<DiscoveryLocation> GetVsCodeDiscoveryLocations()
    {
        var root = GetVsCodeUserPath();
        yield return new DiscoveryLocation("vscode-agents", AssetType.Agent, Path.Combine(root, "agents"));
        yield return new DiscoveryLocation("vscode-prompts", AssetType.Prompt, Path.Combine(root, "prompts"));
        yield return new DiscoveryLocation("vscode-instructions", AssetType.Instruction, Path.Combine(root, "instructions"));
        yield return new DiscoveryLocation("vscode-skills", AssetType.Skill, Path.Combine(GetCopilotHomePath(), "skills"));
    }

    private static IEnumerable<DiscoveryLocation> GetCopilotDiscoveryLocations()
    {
        var root = GetCopilotHomePath();
        yield return new DiscoveryLocation("copilot-agents", AssetType.Agent, Path.Combine(root, "agents"));
        yield return new DiscoveryLocation("copilot-prompts", AssetType.Prompt, Path.Combine(root, "prompts"));
        yield return new DiscoveryLocation("copilot-skills", AssetType.Skill, Path.Combine(root, "skills"));
        yield return new DiscoveryLocation("copilot-instructions", AssetType.Instruction, Path.Combine(root, "instructions"));
    }

    private static IEnumerable<DiscoveryLocation> GetRepoDiscoveryLocations(string localRoot)
    {
        var githubRoot = Path.Combine(localRoot, ".github");
        yield return new DiscoveryLocation("repo-agents", AssetType.Agent, Path.Combine(githubRoot, "agents"));
        yield return new DiscoveryLocation("repo-prompts", AssetType.Prompt, Path.Combine(githubRoot, "prompts"));
        yield return new DiscoveryLocation("repo-skills", AssetType.Skill, Path.Combine(githubRoot, "skills"));
        yield return new DiscoveryLocation("repo-instructions", AssetType.Instruction, Path.Combine(githubRoot, "instructions"));
    }

    private static IEnumerable<DiscoveredAsset> DiscoverAssetsInLocation(DiscoveryLocation location)
    {
        if (location.Type == AssetType.Skill)
        {
            if (!Directory.Exists(location.Root))
            {
                yield break;
            }

            foreach (var directory in Directory.EnumerateDirectories(location.Root)
                         .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                yield return new DiscoveredAsset(location.Type, Path.GetFileName(directory), directory, location.Label);
            }

            yield break;
        }

        if (!Directory.Exists(location.Root))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(location.Root, GetDiscoveryPattern(location.Type), SearchOption.AllDirectories)
                     .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            yield return new DiscoveredAsset(location.Type, GetDiscoveredAssetName(location.Type, file), file, location.Label);
        }
    }

    private static string GetDiscoveryPattern(AssetType type) =>
        type switch
        {
            AssetType.Agent => "*.agent.md",
            AssetType.Prompt => "*.prompt.md",
            AssetType.Instruction => "*.instructions.md",
            _ => "*"
        };

    private static string GetDiscoveredAssetName(AssetType type, string path)
    {
        var fileName = Path.GetFileName(path);
        return type switch
        {
            AssetType.Agent when fileName.EndsWith(".agent.md", StringComparison.OrdinalIgnoreCase) =>
                fileName[..^".agent.md".Length],
            AssetType.Prompt when fileName.EndsWith(".prompt.md", StringComparison.OrdinalIgnoreCase) =>
                fileName[..^".prompt.md".Length],
            AssetType.Instruction when fileName.EndsWith(".instructions.md", StringComparison.OrdinalIgnoreCase) =>
                fileName[..^".instructions.md".Length],
            _ => Path.GetFileNameWithoutExtension(fileName)
        };
    }

    private static CatalogEntryReference? TryMapDiscoveredAsset(CatalogContext context, DiscoveredAsset item)
    {
        var matches = context.Catalog.EnumerateEntries()
            .Where(x => x.Type == item.Type)
            .ToList();

        foreach (var candidate in matches)
        {
            if (!string.IsNullOrWhiteSpace(candidate.Entry.Name)
                && candidate.Entry.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }

            if (TryResolveEntryLocalSourcePath(context.CatalogDirectory, candidate.Entry.Source, out var candidatePath)
                && PathsEqual(candidatePath, item.Path))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool TryResolveEntryLocalSourcePath(string catalogDirectory, string source, out string resolvedPath)
    {
        resolvedPath = string.Empty;
        if (TryParseGitHubSource(source, out _))
        {
            return false;
        }

        var expanded = ExpandConfiguredPath(source);
        resolvedPath = Path.IsPathRooted(expanded) ? expanded : Path.GetFullPath(Path.Combine(catalogDirectory, expanded));
        return File.Exists(resolvedPath) || Directory.Exists(resolvedPath);
    }

    private static bool PathsEqual(string leftPath, string rightPath) =>
        Path.GetFullPath(leftPath).Equals(Path.GetFullPath(rightPath), StringComparison.OrdinalIgnoreCase);

    private static void PrintMigrationReport(MigrationReport report, bool dryRun)
    {
        Console.WriteLine();
        Console.WriteLine(dryRun ? "Dry-run import report" : "Import report");
        Console.WriteLine($"Imported: {report.Imported.Count}");
        Console.WriteLine($"Skipped: {report.Skipped.Count}");
        Console.WriteLine($"Unmapped: {report.Unmapped.Count}");
        Console.WriteLine();

        PrintMigrationSection("Imported", report.Imported);
        PrintMigrationSection("Skipped", report.Skipped);
        PrintMigrationSection("Unmapped", report.Unmapped);
    }

    private static void PrintMigrationSection(string title, IReadOnlyCollection<MigrationReportItem> items)
    {
        Console.WriteLine($"## {title}");
        if (items.Count == 0)
        {
            Console.WriteLine("- none");
            Console.WriteLine();
            return;
        }

        foreach (var item in items)
        {
            Console.WriteLine($"- {item.Asset.Type.ToTypeString()}:{item.Asset.Name} [{item.Asset.Origin}]");
            Console.WriteLine($"  {item.Reason}");
        }

        Console.WriteLine();
    }

    private static string NormalizeCatalogSourceForStorage(string catalogDirectory, string source)
    {
        if (TryParseGitHubSource(source, out _))
        {
            return source.Trim();
        }

        var expanded = ExpandConfiguredPath(source.Trim());
        var resolved = Path.IsPathRooted(expanded) ? expanded : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), expanded));
        if (!File.Exists(resolved) && !Directory.Exists(resolved))
        {
            throw new InvalidOperationException($"Source path does not exist: {resolved}");
        }

        return Path.GetRelativePath(catalogDirectory, resolved);
    }

    private static void PushToLocalSource(
        CatalogEntryReference entryRef,
        string sourcePath,
        string installedPath,
        bool dryRun)
    {
        if (entryRef.Type == AssetType.Skill)
        {
            SyncDirectory(installedPath, sourcePath, dryRun);
            return;
        }

        CopyFileIfNeeded(installedPath, sourcePath, dryRun, overwriteExisting: true);
    }

    private static void PushToGitHub(
        CatalogEntryReference entryRef,
        GitHubSource gitHubSource,
        string installedPath,
        bool dryRun,
        string? message)
    {
        if (!HasGitHubToken())
        {
            throw new InvalidOperationException("GitHub-backed push requires GITHUB_TOKEN or GH_TOKEN.");
        }

        if (entryRef.Type == AssetType.Skill)
        {
            PushDirectoryToGitHub(gitHubSource, installedPath, dryRun, message, entryRef.Entry.Name);
            return;
        }

        PushFileToGitHub(gitHubSource, installedPath, dryRun, message, entryRef.Entry.Name);
    }

    private static void PushFileToGitHub(
        GitHubSource gitHubSource,
        string localFilePath,
        bool dryRun,
        string? message,
        string entryName)
    {
        if (!File.Exists(localFilePath))
        {
            throw new InvalidOperationException($"Local file to push was not found: {localFilePath}");
        }

        if (dryRun)
        {
            Console.WriteLine($"Would push {localFilePath} to GitHub path {gitHubSource.Path} on {gitHubSource.Owner}/{gitHubSource.Repository}@{gitHubSource.Reference}");
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

    private static void PushDirectoryToGitHub(
        GitHubSource gitHubSource,
        string localDirectoryPath,
        bool dryRun,
        string? message,
        string entryName)
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

            if (dryRun)
            {
                Console.WriteLine($"Would push {relativePath} to GitHub path {remotePath}");
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

    private static string? GetGitHubContentSha(string owner, string repo, string path, string reference)
    {
        var endpoint = BuildGitHubContentsApiUrl(owner, repo, path, reference);
        using var request = CreateGitHubRequest(HttpMethod.Get, endpoint);
        using var response = GitHubClient.Send(request);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        EnsureGitHubSuccess(response, "read GitHub file metadata");
        using var json = JsonDocument.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        return json.RootElement.TryGetProperty("sha", out var shaProperty) ? shaProperty.GetString() : null;
    }

    private static void PutGitHubFile(
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
        request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        using var response = GitHubClient.Send(request);
        EnsureGitHubSuccess(response, "push GitHub content");
    }

    private static ResolvedAssetSource DownloadGitHubSource(CatalogEntryReference entryRef, GitHubSource gitHubSource)
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

    private static void DownloadGitHubFile(GitHubSource gitHubSource, string localFilePath)
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

    private static void DownloadGitHubDirectory(GitHubSource gitHubSource, string localDirectoryPath)
    {
        DownloadGitHubDirectoryCore(gitHubSource.Owner, gitHubSource.Repository, gitHubSource.Reference, gitHubSource.Path, localDirectoryPath);
    }

    private static void DownloadGitHubDirectoryCore(
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

    private static void DownloadUrlToFile(string url, string localFilePath)
    {
        using var response = SendGitHubRequest(HttpMethod.Get, url);
        EnsureGitHubSuccess(response, "download GitHub raw content");
        Directory.CreateDirectory(Path.GetDirectoryName(localFilePath)
                                  ?? throw new InvalidOperationException("Unable to create GitHub temp directory."));
        File.WriteAllBytes(localFilePath, response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult());
    }

    private static HttpResponseMessage SendGitHubRequest(HttpMethod method, string url)
    {
        using var request = CreateGitHubRequest(method, url);
        return GitHubClient.Send(request);
    }

    private static HttpRequestMessage CreateGitHubRequest(HttpMethod method, string url)
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

    private static HttpClient CreateGitHubClient()
    {
        return new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
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

    private static bool TryParseGitHubSource(string source, out GitHubSource gitHubSource)
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

    private static string ExpandConfiguredPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        if (path.StartsWith("~/.copilot", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("~\\.copilot", StringComparison.OrdinalIgnoreCase))
        {
            var copilotHome = GetCopilotHomePath();
            var remainder = path["~/.copilot".Length..].TrimStart('/', '\\');
            if (string.IsNullOrWhiteSpace(remainder))
            {
                return copilotHome;
            }

            remainder = remainder.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            return Path.Combine(copilotHome, remainder);
        }

        if (path.Equals("~", StringComparison.Ordinal))
        {
            return GetUserHomePath();
        }

        if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
        {
            var home = GetUserHomePath();
            var remainder = path[2..].Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            return Path.Combine(home, remainder);
        }

        return Environment.ExpandEnvironmentVariables(path);
    }

    private static string GetUserHomePath() =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static string GetCopilotHomePath()
    {
        var configured = Environment.GetEnvironmentVariable("COPILOT_HOME");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return ExpandConfiguredPath(configured);
        }

        return Path.Combine(GetUserHomePath(), ".copilot");
    }

    private static string GetVsCodeUserPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Code", "User");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Path.Combine(GetUserHomePath(), "Library", "Application Support", "Code", "User");
        }

        return Path.Combine(GetUserHomePath(), ".config", "Code", "User");
    }

    private static bool HasGitHubToken() => !string.IsNullOrWhiteSpace(GetGitHubToken());

    private static string? GetGitHubToken()
    {
        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrWhiteSpace(token))
        {
            return token;
        }

        return Environment.GetEnvironmentVariable("GH_TOKEN");
    }

    private static IEnumerable<string> SplitOptionValues(string value)
    {
        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x));
    }

    private static string ActionVerb(string action, bool dryRun) =>
        dryRun ? $"Would {action}" : CultureAwareCapitalize(action);

    private static string CultureAwareCapitalize(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];

    private static string EscapePipe(string? value) => (value ?? string.Empty).Replace("|", "\\|");

    private static int Execute(Func<int> action)
    {
        try
        {
            return action();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
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

    public List<CatalogEntry> GetEntries(AssetType type) =>
        type switch
        {
            AssetType.Agent => Catalog.Agents,
            AssetType.Prompt => Catalog.Prompts,
            AssetType.Skill => Catalog.Skills,
            AssetType.Instruction => Catalog.Instructions,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

    public void AddEntry(AssetType type, CatalogEntry entry)
    {
        GetEntries(type).Add(entry);
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

internal sealed class InstalledCatalogItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

internal sealed record CatalogEntryReference(AssetType Type, CatalogEntry Entry);

internal sealed record ExecutionOptions(bool DryRun, bool Force);

internal sealed record DiscoveredAsset(AssetType Type, string Name, string Path, string Origin);

internal sealed record DiscoveryLocation(string Label, AssetType Type, string Root);

internal sealed class MigrationReport
{
    public List<MigrationReportItem> Imported { get; } = [];
    public List<MigrationReportItem> Skipped { get; } = [];
    public List<MigrationReportItem> Unmapped { get; } = [];
}

internal sealed record MigrationReportItem(DiscoveredAsset Asset, string Reason);

internal sealed record GitHubSource(
    string Owner,
    string Repository,
    string Reference,
    string Path,
    GitHubContentType ContentType,
    string OriginalUrl);

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

internal sealed class GitHubPutContentRequest
{
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("branch")]
    public string Branch { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    [JsonPropertyName("sha")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Sha { get; init; }
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
