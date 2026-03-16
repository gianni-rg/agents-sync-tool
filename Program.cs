using System.CommandLine;

class Program
{
    private static readonly string[] DefaultPatterns = ["*.agent.md", "*.prompt.md"];

    static int Main(string[] args)
    {
        var rootCommand = new RootCommand("Syncs custom agents and prompts from a central catalog to your repo");

        var defaultSyncCommand = new Command("default", "Syncs agents from VS Code user folder to Copilot user folder");

        var catalogPathOption = new Option<DirectoryInfo?>("--catalog", "-c")
        {
            Description = "Path to the source catalog root. Defaults to VS Code or GitHub Copilot user folders if omitted.",
            Required = false
        };

        var localPathOption = new Option<DirectoryInfo>("--local", "-l")
        {
            Description = "Local repo path to sync into (default: current directory)",
            Required = false
        };

        var dryRunOption = new Option<bool>("--dry-run", "-d")
        {
            Description = "Show what would be copied without making changes"
        };

        var defaultDryRunOption = new Option<bool>("--dry-run", "-d")
        {
            Description = "Show what would be copied without making changes"
        };

        var patternsOption = new Option<string[]>("--pattern", "-p")
        {
            Description = "File pattern(s) to sync. Repeat --pattern for multiple values. Default: *.agent.md and *.prompt.md",
            Required = false
        };

        rootCommand.Options.Add(catalogPathOption);
        rootCommand.Options.Add(localPathOption);
        rootCommand.Options.Add(dryRunOption);
        rootCommand.Options.Add(patternsOption);

        defaultSyncCommand.Options.Add(defaultDryRunOption);
        rootCommand.Subcommands.Add(defaultSyncCommand);

        rootCommand.SetAction(parseResult =>
        {
            var catalogPath = parseResult.GetValue(catalogPathOption)!;
            var localPath = parseResult.GetValue(localPathOption);
            var dryRun = parseResult.GetValue(dryRunOption);
            var patterns = parseResult.GetValue(patternsOption);

            var resolvedCatalogPath = ResolveCatalogPath(catalogPath);
            if (resolvedCatalogPath is null)
            {
                Console.Error.WriteLine("Unable to find source catalog path.");
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

            Console.WriteLine($"Syncing from catalog: {resolvedCatalogPath.FullName}");
            Console.WriteLine($"To local repo: {localDir}");
            Console.WriteLine($"Patterns: {string.Join(", ", effectivePatterns)}");

            SyncFolder(agentsSrc, agentsDest, dryRun, effectivePatterns);
            SyncFolder(promptsSrc, promptsDest, dryRun, effectivePatterns);

            Console.WriteLine("Sync complete!");
            return 0;
        });

        defaultSyncCommand.SetAction(parseResult =>
        {
            var dryRun = parseResult.GetValue(defaultDryRunOption);
            var vscodeUserPath = GetVsCodeUserPath();
            var copilotUserPath = GetCopilotUserPath();

            if (!Directory.Exists(vscodeUserPath))
            {
                Console.Error.WriteLine($"VS Code user folder not found: {vscodeUserPath}");
                return 1;
            }

            if (!Directory.Exists(copilotUserPath))
            {
                Directory.CreateDirectory(copilotUserPath);
            }

            var vscodeAgents = Path.Combine(vscodeUserPath, "prompts");
            var copilotAgents = Path.Combine(copilotUserPath, "agents");

            Console.WriteLine($"Syncing agents from: {vscodeAgents}");
            Console.WriteLine($"To: {copilotAgents}");

            SyncFolder(vscodeAgents, copilotAgents, dryRun, ["*.agent.md"]);

            Console.WriteLine("Default sync complete!");
            return 0;
        });

        return rootCommand.Parse(args).Invoke();
    }

    static void SyncFolder(string src, string dest, bool dryRun, IReadOnlyCollection<string> patterns)
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
            bool needsUpdate = !exists ||
                File.GetLastWriteTime(srcFile) > File.GetLastWriteTime(destFile);

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

        // Delete files in dest not in src
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

    static IReadOnlyCollection<string> GetEffectivePatterns(string[]? rawPatterns)
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

    static IEnumerable<string> EnumerateMatchingFiles(string root, IReadOnlyCollection<string> patterns)
    {
        return patterns
            .SelectMany(pattern => Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    static DirectoryInfo? ResolveCatalogPath(DirectoryInfo? catalogPath)
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

    static string GetVsCodeUserPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Code", "User");
    }

    static string GetCopilotUserPath()
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
}
