# Agents Sync Tool

A .NET global tool that syncs agents and prompts from a central catalog to any repo on demand.

It copies only changed `.agent.md` and `.prompt.md` files, deletes obsolete ones, and supports `--dry-run` for safety.

## Quick setup

1. Clone this repo and navigate to it

1. Build and install:

    ```powerShell
    dotnet pack
    dotnet tool install --global --add-source nupkg/ AgentSync
    ```

1. Usage:

    ```powerShell
    # Dry run first
    AgentSync -c /path/to/your/org-catalog -l /path/to/target/repo --dry-run

    # Real sync
    AgentSync -c /path/to/your/org-catalog -l /path/to/target/repo

    # Sync Visual Studio Code agents to GitHub Copilot CLI agents
    AgentSync default
    ```

## Key features

- Syncs `catalog/agents/` → `repo/.github/agents/`
- Syncs `catalog/prompts/` → `repo/.github/prompts/`
- Only copies newer/changed files, preserves repo-specific ones
- Deletes files in target that no longer exist in catalog
- Handles subfolders (e.g., `agents/dotnet/`, `prompts/testing/`)
- Works anywhere (current dir or `--local`)
- `default` command syncs VS Code agents to GitHub Copilot CLI agents

## Contribution

The project is constantly evolving and contributions are warmly welcomed.

I'm more than happy to receive any kind of contribution to this experimental project: from helpful feedbacks to bug reports, documentation, usage examples, feature requests, or directly code contribution for bug fixes and new and/or improved features.

Feel free to file issues and pull requests on the repository and I'll address them as much as I can, *with a best effort approach during my spare time*. DO NOT expect a super fast turnaround, but I'll do my best to keep the project active and responsive.

> Development is mainly done on Windows, so other platforms are not directly developed, tested, or supported. Help is kindly appreciated in making the libraries work on other platforms as well.

## License

This project is licensed under the [Apache License 2.0](./LICENSE).

Copyright © 2026 Gianni Rosa Gallina.
