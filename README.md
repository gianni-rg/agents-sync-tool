# Agents Sync Tool

A .NET global tool that manages GitHub Copilot assets from a `catalog.json` repository.

`AgentSync` is fully catalog-first. Use `import` to bring existing unmanaged assets under tracked management.

## Command surface

- `AgentSync list` - list catalog entries and install status
- `AgentSync search` - search by name, description, and tags
- `AgentSync use` - install one asset and its typed dependencies
- `AgentSync install` - install all assets or a selected subset
- `AgentSync sync` - refresh tracked assets from their catalog sources
- `AgentSync import` - discover unmanaged assets and bring them under management
- `AgentSync remove` - uninstall a managed asset and clear tracked state
- `AgentSync add` - register a new asset in `catalog.json`
- `AgentSync push` - push local managed changes back to local or GitHub-backed sources

## Quick setup

1. Clone this repo and navigate to it.
2. Build and install:

   ```powershell
   dotnet pack src\AgentSync\AgentSync.csproj
   dotnet tool install --global --add-source nupkg/ AgentSync
   ```

3. Run commands against a catalog repository:

   ```powershell
   # List catalog contents
   AgentSync list --catalog D:\Personal\agents-catalog --local .

   # Install one asset and its dependencies into the repo
   AgentSync use agent-architect --type agent --catalog D:\Personal\agents-catalog --local .

   # Install everything for a user-scoped target
   AgentSync install --catalog D:\Personal\agents-catalog --scope user

   # Refresh previously installed assets for this repo
   AgentSync sync --catalog D:\Personal\agents-catalog --local .

   # Import unmanaged assets into catalog-aware state
   AgentSync import --catalog D:\Personal\agents-catalog --local .
   ```

## Catalog model

`AgentSync` expects a `catalog.json` file, either directly or in the directory passed to `--catalog`.

```json
{
  "version": 1,
  "targets": {
    "copilot": {
      "repo": {
        "agents": ".github/agents",
        "prompts": ".github/prompts",
        "skills": ".github/skills",
        "instructions": ".github/instructions"
      }
    }
  },
  "catalog": {
    "agents": [],
    "prompts": [],
    "skills": [],
    "instructions": []
  }
}
```

Supported entry fields today:

- `name`
- `description`
- `source`
- `requires`
- `tags`

## Current capabilities

- Reads `catalog.json`
- Lists assets with install status
- Installs one asset plus typed dependencies
- Installs all assets or a named subset for a target
- Syncs previously installed assets using a local state file
- Imports unmanaged assets into tracked install state
- Searches catalog entries by metadata
- Removes tracked assets from the target
- Registers new assets in `catalog.json`
- Pushes local managed changes back to their sources
- Supports repo and user target scopes from the catalog
- Supports local and relative source paths
- Supports GitHub browser and raw URLs as remote sources
- Supports `--dry-run` for install, sync, import, remove, and push operations
- Supports `--force` when installing or importing over unmanaged content
- Respects `COPILOT_HOME` when resolving `~/.copilot` targets

## Internal architecture

`AgentSync` is a single-project modular architecture under `src\AgentSync`.

- `src/AgentSync/Program.cs` is bootstrap only
- `src/AgentSync/Composition/` contains DI setup, root command assembly, shared CLI options, and command execution helpers
- `src/AgentSync/Features/<Command>/` contains one vertical slice per verb such as `list`, `search`, `use`, `install`, `sync`, `import`, `remove`, `add`, and `push`
- `src/AgentSync/Domain/` contains shared enums, records, catalog models, install-state models, discovery models, and source models
- `src/AgentSync/Application/` contains catalog lookup, context creation, install-state persistence coordination, and workflow orchestration
- `src/AgentSync/Infrastructure/` contains filesystem, discovery, local-source, and GitHub-backed source services
- `src/AgentSync/Presentation/` contains the terminal rendering abstraction and the `Spectre.Console` implementation
- `tests/AgentSync.Tests/` contains regression coverage for validation, path resolution, install-state workflows, discovery mapping, GitHub URL parsing, and presentation handoff seams

When adding a new command:

- add a new feature slice under `src/AgentSync/Features/`
- register it in `src/AgentSync/Composition/ServiceCollectionExtensions.cs`
- keep shared option semantics in `src/AgentSync/Composition/CommandOptions.cs`
- place shared models in `src/AgentSync/Domain/`
- place reusable orchestration in `src/AgentSync/Application/`
- place external side effects in `src/AgentSync/Infrastructure/`
- render user-facing output through `src/AgentSync/Presentation/`

## Common workflows

### Repo install

```powershell
AgentSync use agent-architect --type agent --catalog D:\Personal\agents-catalog --local .
```

### User install

```powershell
AgentSync install --catalog D:\Personal\agents-catalog --scope user --platform copilot
```

### Migration from unmanaged assets

```powershell
# Auto-scan known locations
AgentSync import --catalog D:\Personal\agents-catalog --local .

# Or scan a specific unmanaged root
AgentSync import --catalog D:\Personal\agents-catalog --local . --source D:\temp\unmanaged-assets
```

By default, `import` only migrates discovered assets that already map cleanly to existing catalog entries.

If you previously automated unmanaged-asset onboarding with `migrate`, replace those invocations with `import`. The onboarding workflow is the same, but `import` is now the only exposed command for it.

To automatically add unmapped discovered assets into `catalog.json` before importing them, use `--add-unmapped`:

```powershell
AgentSync import --catalog D:\Personal\agents-catalog --local . --source D:\temp\unmanaged-assets --add-unmapped
```

Combine `--add-unmapped` with `--dry-run` to preview both planned catalog additions and managed installs without persisting either change:

```powershell
AgentSync import --catalog D:\Personal\agents-catalog --local . --source D:\temp\unmanaged-assets --add-unmapped --dry-run
```

### Remote GitHub-backed sources

Catalog entries can point at supported GitHub sources such as:

- `https://github.com/<owner>/<repo>/blob/<branch>/<path>`
- `https://github.com/<owner>/<repo>/tree/<branch>/<path>`
- `https://raw.githubusercontent.com/<owner>/<repo>/<branch>/<path>`

Example:

```powershell
AgentSync add cli-readme `
  --type instruction `
  --source https://github.com/octocat/Hello-World/blob/master/README `
  --description "Remote README example" `
  --catalog D:\Personal\agents-catalog
```

For private repositories, set `GITHUB_TOKEN` or `GH_TOKEN` before running `use`, `sync`, or `push`.

## Conflict behavior

- `use`, `install`, and `import` refuse to overwrite unmanaged content unless you pass `--force`
- `sync` refreshes tracked assets from the catalog source of truth
- `remove` deletes the installed target and unregisters the asset from tracked state

## Known limitations

- Directory pushes to GitHub update and create files, but do not delete remote files that no longer exist locally.
- Platform-specific target discovery still depends on the paths configured in `catalog.json`.

## Acknowledgements

Inspired by [The Library Meta-Skill](https://github.com/disler/the-library) and by the idea of having a structured way to share agents, skills, and prompts across projects and teams.

## Contribution

The project is constantly evolving and contributions are warmly welcomed.

I'm more than happy to receive any kind of contribution to this experimental project: from helpful feedbacks to bug reports, documentation, usage examples, feature requests, or directly code contribution for bug fixes and new and/or improved features.

Feel free to file issues and pull requests on the repository and I'll address them as much as I can, *with a best effort approach during my spare time*. DO NOT expect a super fast turnaround, but I'll do my best to keep the project active and responsive.

> Development is mainly done on Windows, but the tool is being shaped toward a cross-platform `.NET` global tool workflow. Help improving and validating non-Windows paths is very welcome.

## License

This project is licensed under the [Apache License 2.0](./LICENSE).

Copyright © 2026 Gianni Rosa Gallina.
