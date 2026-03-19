# Tools

## Serena MCP: How to use it

## Serena MCP: How agents should use it

Serena is an MCP server that gives you **IDE‑like powers** over this project: semantic navigation, code understanding, and safe file edits via Language Server Protocol (LSP). Always prefer Serena tools over guessing from memory when working with this repo.

### 1. Initialization (do this first in each project)

When Serena is available, follow this sequence before using any other tools:

1. Load Serena’s instructions:
    - Call `serena.initial_instructions` (or equivalent) and follow its guidance.
2. Activate the project:
    - Call `serena.activate_project` with the project root path (usually the current working directory).
    - Only consider Serena “ready” once activation succeeds.
3. Check onboarding:
    - Call `serena.check_onboarding_performed`.
    - If onboarding has not been done, let Serena perform it so it can build its internal understanding/memories for this repo.[^4]

If activation or onboarding fails, ask the user to fix the setup (paths, config, LSP) instead of proceeding blindly.

### 2. When to use Serena vs model-only reasoning

Use Serena tools whenever the task involves this codebase:

- Use Serena for:
  - Finding code: `find_symbol`, `find_referencing_symbols`, `list_files`, `read_file`.
  - Editing code: `edit_file`, `create_text_file`, `delete_lines`.
  - Running commands: `execute_shell_command` (tests, lint, build).
  - Long tasks: `write_memory` / `read_memory` for plans and architecture notes.

Avoid relying on your own “memory” of files. Always fetch the newest code with Serena before proposing or applying changes.

### 3. Typical workflows

For any non-trivial coding task, follow a **plan → inspect → edit → verify** loop using Serena:

- Refactors:

1. `find_symbol` / `find_referencing_symbols` to map impact.
2. `read_file` to inspect real code.
3. Propose a stepwise plan in natural language.
4. Apply modifications with `edit_file` only.
5. Verify with `read_file` and `execute_shell_command` (tests, `git diff`).

- Bug investigations:

1. Locate entrypoints and relevant functions using Serena navigation tools.
2. Build a small call graph based on actual code you read.
3. Suggest minimal fixes and apply them via `edit_file`.
4. Run targeted tests/commands via `execute_shell_command` and adapt based on output.

- Feature implementation:

1. Use Serena to explore existing patterns (e.g., similar features, API handlers).
2. Draft a design/implementation plan.
3. Implement in small, reviewable steps with `edit_file` and new files via `create_text_file`.
4. Run tests, lint, or build commands each step and report results.

### 4. Safety and scope

- Respect project settings in `.serena/project.yml`:
  - If `read_only` is true, do not attempt to edit files or run shell commands; limit yourself to navigation and analysis.
- Keep edits **small and reversible**:
  - Prefer many small `edit_file` calls instead of one huge change.
  - After each logical change, verify using tests or lightweight commands via `execute_shell_command`.

If you are unsure about a large change, ask the user to confirm the plan before applying edits.

### 5. Working with memories

For long-running or complex tasks in this project:

- Use `write_memory` to store:
  - Architecture overviews.
  - Multi-step refactor plans.
  - Known caveats or domain rules specific to this repo.
- At the start of a new session, call:
  - `list_memories` and `read_memory` for relevant keys before continuing work.

This keeps project knowledge persistent beyond a single conversation and reduces repeated rediscovery.

## Markdown Guidelines

I don't like em-dashes — they feel too formal and heavy for our style. Instead, prefer simple hyphens for lists and separators. Use them only when necessary for clarity, and avoid overusing them in running text.
I also don't like emoticons. Avoid overusing them in running text.

Preferred practices for fenced code blocks and language tags:

- Always add a language after the opening fence when possible (for syntax highlighting and tooling).
  - Examples: ```python,```bash, ```json,```yaml
- For plain ASCII diagrams, trees, or text that has no programming language, use ```text
- If you must include non-language content, prefer labeling it as `text` rather than leaving it unlabeled.

Examples

```python
def hello():
    print("hello")

```

```bash
docker-compose up -d

```

```text
PRODUCT_VISION.md (Central Hub - Required reading)
    ├── Informs COMPETITIVE_ANALYSIS.md
    └── Enablers ELEVATOR_PITCH.md
```

Use `markdownlint` CLI tool to identify and fix any issues in Markdown files. Formatting errors, broken links, inconsistent styling and any other issue reported by markdownlint CLI tool. The tools is already installed in the system, no need to install it or use other tools. After all the issues are fixed, stop, do not propose to fix other Markdown files. If some rule is complex to fix, try to fix it as much as possible, but do not propose to disable the rule. If some issue is not possible to fix, explain why it cannot be fixed and propose a workaround if possible. Do not propose to disable the rule, only propose to disable the rule if the issue cannot be fixed and there is no workaround.
