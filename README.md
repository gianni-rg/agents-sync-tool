# Agents Sync Tool

A .NET global tool that installs and syncs Copilot assets from a `catalog.json` repository.

The current catalog-aware command set is:

- `AgentSync list`
- `AgentSync use`
- `AgentSync sync`
- `AgentSync default`

The tool also keeps the older legacy direct-folder sync behavior when run without a subcommand.

## Quick setup

1. Clone this repo and navigate to it

1. Build and install:

    ```powershell
    dotnet pack
    dotnet tool install --global --add-source nupkg/ AgentSync
    ```

1. Usage:

    ```powershell
    # List catalog contents
    AgentSync list --catalog D:\Personal\agents-catalog --local .

    # Install one asset and its dependencies
    AgentSync use agent-architect --type agent --catalog D:\Personal\agents-catalog --local .

    # Refresh previously installed assets for this repo
    AgentSync sync --catalog D:\Personal\agents-catalog --local .

    # Sync Visual Studio Code agents to GitHub Copilot CLI agents
    AgentSync default
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
- Syncs previously installed assets using a local state file
- Supports repo and user target scopes from the catalog
- Supports local and relative source paths
- Supports `--dry-run` for install and sync operations
- Preserves the existing `default` command

## Not implemented yet

- GitHub URL sources
- `add`, `push`, `remove`, and `search`
- richer conflict detection and diff reporting
- platform-specific target path discovery beyond catalog configuration

## Contribution

The project is constantly evolving and contributions are warmly welcomed.

I'm more than happy to receive any kind of contribution to this experimental project: from helpful feedbacks to bug reports, documentation, usage examples, feature requests, or directly code contribution for bug fixes and new and/or improved features.

Feel free to file issues and pull requests on the repository and I'll address them as much as I can, *with a best effort approach during my spare time*. DO NOT expect a super fast turnaround, but I'll do my best to keep the project active and responsive.

> Development is mainly done on Windows, but the tool is being shaped toward a cross-platform `.NET` global tool workflow. Help improving and validating non-Windows paths is very welcome.

## License

This project is licensed under the [Apache License 2.0](./LICENSE).

Copyright © 2026 Gianni Rosa Gallina.
