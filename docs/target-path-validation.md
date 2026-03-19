# Target Path Validation

Research performed on March 19, 2026 to validate every path in the AgentSync `targets` block against official GitHub Copilot CLI documentation and VS Code documentation.

## Sources consulted

- [GitHub Copilot customization cheat sheet](https://docs.github.com/en/copilot/reference/customization-cheat-sheet)
- [Copilot CLI command reference](https://docs.github.com/en/copilot/reference/copilot-cli-reference/cli-command-reference) - skill locations table and custom agent locations table
- [Create custom agents for CLI](https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/create-custom-agents-for-cli)
- GitHub `docs` repository source: `create-skills.md`, `create-custom-agents-for-cli.md`, `add-custom-instructions.md`, `cli-best-practices.md`, `cli-command-reference.md`
- Local `copilot help commands; copilot help config; copilot help environment` output (cross-validation)

## Validation results

### Copilot platform - repo scope

| Target key | Path | Status | Notes |
| --- | --- | --- | --- |
| `copilot.repo.agents` | `.github/agents` | Valid | Confirmed in both CLI docs and cheat sheet |
| `copilot.repo.prompts` | `.github/prompts` | Valid | Official VS Code IDE path; stored in repo for version control; no native CLI concept |
| `copilot.repo.skills` | `.github/skills` | Valid | Confirmed in CLI command reference skills location table |
| `copilot.repo.instructions` | `.github/instructions` | Valid | Covers path-specific instructions (`.github/instructions/**/*.instructions.md`); does not replace `.github/copilot-instructions.md` |

### Copilot platform - user scope

| Target key | Path | Status | Notes |
| --- | --- | --- | --- |
| `copilot.user.agents` | `~/.copilot/agents` | Valid | Confirmed in CLI docs, CLI command reference, and `copilot help` output |
| `copilot.user.prompts` | `~/.copilot/prompts` | **Not documented** | No official source defines this path for the CLI; prompt files are an IDE feature; no evidence found in CLI docs, environment variables, or `copilot help` output |
| `copilot.user.skills` | `~/.copilot/skills` | Valid | Confirmed in CLI command reference skills locations table |
| `copilot.user.instructions` | `~/.copilot/instructions` | Partial | VS Code reads this directory for user custom instructions; the Copilot CLI global instructions path is a single file: `$HOME/.copilot/copilot-instructions.md` - the directory form is not natively picked up by the CLI without `COPILOT_CUSTOM_INSTRUCTIONS_DIRS` |

### VS Code platform - user scope

> All paths below assume the default VS Code profile on Windows. Named profiles use
> `%APPDATA%\Code\User\profiles\<profile-hash>\` instead.

| Target key | Path | Status | Notes |
| --- | --- | --- | --- |
| `vscode.user.agents` | `%APPDATA%\Code\User\agents` | Valid | Default VS Code profile on Windows |
| `vscode.user.prompts` | `%APPDATA%\Code\User\prompts` | Valid | Default VS Code profile on Windows |
| `vscode.user.skills` | `%USERPROFILE%\.copilot\skills` | Valid | Same physical path as `copilot.user.skills`; VS Code Copilot extension reads from `~/.copilot/skills` |
| `vscode.user.instructions` | `%APPDATA%\Code\User\instructions` | Valid | Default VS Code profile on Windows |

## Key issues

### 1. `copilot.user.prompts` has no documented backing

`~/.copilot/prompts` is cited nowhere in GitHub Copilot CLI documentation, the CLI
command reference, the customization cheat sheet, or in `copilot help` output. Prompt files
(`.prompt.md`) are a VS Code IDE feature in public preview and have no user-level CLI
equivalent. This path should be removed or left null for the `copilot` platform.

### 2. `copilot.user.instructions` is a directory, CLI expects a single file

The Copilot CLI global instructions convention is `$HOME/.copilot/copilot-instructions.md`
(one file). Syncing files into `~/.copilot/instructions/` (a directory) will not be
automatically picked up by the CLI unless the user also sets `COPILOT_CUSTOM_INSTRUCTIONS_DIRS`
to include that path. The directory form is correct for VS Code's reading of user
instructions, so the path itself is fine for mixed setups - just document the caveat.

### 3. Non-default VS Code profiles

All `vscode.user.*` paths resolve to `%APPDATA%\Code\User\` which is only correct for the
default VS Code profile. Users running a named profile would need a different path. This is
a known limitation that requires out-of-band configuration or a future profile-aware feature.

## Revised targets block

See the recommended revised block in the next section of this document or in the
[Revised targets proposal](#revised-targets-block-proposal) below.

## Revised targets block proposal

Changes from the original:

- Removed `copilot.user.prompts` - no official CLI backing
- Kept `copilot.user.instructions` with a note about the single-file vs directory distinction
- Kept all four `copilot.repo.*` paths (repo-level `.github/prompts` is a valid version-control store even if the CLI ignores it)
- All `vscode.user.*` paths unchanged and confirmed valid

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
      },
      "user": {
        "agents": "~/.copilot/agents",
        "skills": "~/.copilot/skills",
        "instructions": "~/.copilot/instructions"
      }
    },
    "vscode": {
      "repo": {
        "agents": ".github/agents",
        "prompts": ".github/prompts",
        "instructions": ".github/instructions"
      },
      "user": {
        "agents": "%APPDATA%\\Code\\User\\agents",
        "prompts": "%APPDATA%\\Code\\User\\prompts",
        "skills": "~/.copilot/skills",
        "instructions": "%APPDATA%\\Code\\User\\instructions"
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

### What changed and why

| Change | Reason |
| --- | --- |
| Removed `copilot.user.prompts` | No official CLI path exists for user-level prompt files |
| `copilot` and `vscode` are separate platform keys | Avoids conflating two different path conventions in one block |
| `vscode.repo.*` omits `skills` | VS Code does not have a dedicated repo-scoped skills directory separate from Copilot CLI's `.github/skills`; route skill installs through the `copilot` platform for repo scope |
| `vscode.user.skills` points to `~/.copilot/skills` | VS Code Copilot extension reads from `~/.copilot/skills`; same backing as `copilot.user.skills` - no duplication needed from a filesystem perspective, but the platform key makes intent explicit in catalog tooling |

### Notes on cross-platform paths

- `~/.copilot/*` paths work on Windows via AgentSync's path-expansion helpers, which
  resolve `~` to `%USERPROFILE%` and honor `COPILOT_HOME` when it is set.
- `%APPDATA%` paths are Windows-specific. On Linux/macOS, VS Code stores its user profile
  under `~/.config/Code/User/` (Linux) or `~/Library/Application Support/Code/User/`
  (macOS). A future enhancement could detect the OS and substitute the right base path.
- AgentSync now reads `COPILOT_HOME` when resolving `~/.copilot/*` targets.
