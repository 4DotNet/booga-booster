---
name: ai-tooling-sync
description: >-
  Keeps this repo's AI tooling working identically for Claude Code and GitHub Copilot
  (Copilot CLI, VS Code, Visual Studio). Use whenever adding, renaming, editing or
  removing an agent (.claude/agents), skill (.claude/skills), slash command
  (.claude/commands or .github/prompts), MCP server (.mcp.json), the distributable
  plugin (plugins/), or the repo-wide instructions (CLAUDE.md,
  .github/copilot-instructions.md) — and when asked whether Claude and Copilot are in
  sync, or why an agent/skill/command/MCP server works in one tool but not the other.
  Runs a drift check, regenerates the mechanical mirrors, and fixes what needs judgement
  by hand.
---

# AI tooling sync — Claude Code ⇄ GitHub Copilot

This repo must work equally well for a Claude Code user and a GitHub Copilot user, in
the terminal and in the IDE. Every asset has **one canonical source**; where a product
cannot read that source, a **mirror** is derived from it. Never edit a mirror by hand
and never let the two drift.

## Who reads what

| Asset | Canonical source | Claude Code | Copilot CLI | VS Code (Copilot harness) | Visual Studio 2026 | Copilot cloud agent (github.com) | Mirror |
| --- | --- | :-: | :-: | :-: | :-: | :-: | --- |
| MCP servers | `.mcp.json` (`mcpServers`) | ✅ | ✅ | ✅ | ❌ (solution dir only) | ❌ (repo settings) | `src/.mcp.json` (`servers`) |
| Agents | `.claude/agents/*.md` | ✅ | ✅ | ✅ | ❌ | ❌ | `.github/agents/<name>.agent.md` (generated, **every** agent) |
| Skills | `.claude/skills/<name>/SKILL.md` | ✅ | ✅ | ❓ (documents `.github/skills` only) | ✅ | ✅ | `.github/skills/<name>/` (copied, **every** skill) |
| Slash commands | `.claude/commands/<ns>/<name>.md` | ✅ | ✅ | ❌ | ❌ | ❌ | `.github/prompts/<ns>-<name>.prompt.md` |
| Instructions | `CLAUDE.md` | ✅ | ✅ | ✅ | ❌ | ✅ | `.github/copilot-instructions.md` (condensed, hand-written) |
| Plugin | `.claude/{agents,skills}`, `.mcp.json` | ✅ | ✅ | — | — | — | `plugins/4dotnet-boogabooster/` |

Every Copilot surface is therefore covered by `.github/` alone, and Claude Code by
`.claude/` alone. Copilot CLI reads both and, on a name clash, prefers `.github` — so the
generated mirror is what Copilot runs, and it must be equivalent to the canonical file.

Facts behind the table (verified 2026-11-23 against the docs listed in *Keeping this
skill true*; re-verify when a product changes):

- **VS Code** now runs sessions on a *harness*: **Copilot** (the Copilot CLI runtime —
  the recommended one), **Claude** (Claude Agent SDK, reads `.claude/` natively),
  Codex, or **Local** (the old chat, deprecated, removed Feb 2027). For the Copilot
  harness the docs list `.github/skills` and `.github/agents`; `.claude/*` is only
  documented for Local. Prompt files (`.github/prompts`) run only in Local — the docs say
  to convert them to skills, which is why every `/opsx:*` command also exists as an
  `openspec-*` skill. MCP: the root `.mcp.json` (`mcpServers`); `.vscode/mcp.json` is
  Local-only and deprecated.
- **Visual Studio 2026** reads agents only from `.github/agents/*.agent.md` (frontmatter
  `name`, `description`, `model`, `tools`; its own tool names such as `readfile`,
  `editfiles`, `code_search` — unknown names are ignored), skills from `.github/skills`,
  `.claude/skills` and `.agents/skills`, instructions only from
  `.github/copilot-instructions.md` and `*.instructions.md`, and MCP only from
  `%USERPROFILE%\.mcp.json` and `<SOLUTIONDIR>\{.mcp.json,.vs\mcp.json,.vscode\mcp.json}`
  with a `servers` key. The solution is `src/BoogaBooster.slnx`, so the root `.mcp.json`
  is invisible to it — hence the `src/.mcp.json` mirror.
- **Copilot cloud agent (github.com)** documents agents only under `.github/agents`;
  skills from `.github/skills`, `.claude/skills`, `.agents/skills`; instructions from
  `CLAUDE.md`, `AGENTS.md` and `copilot-instructions.md`. Its MCP servers are configured
  in the repository's settings on github.com, not from a file — a maintainer must mirror
  `.mcp.json` there by hand.
- **Copilot CLI** reads agents from `.github/agents` **and** `.claude/agents`, skills from
  `.github/skills`, `.agents/skills` and `.claude/skills`, commands from
  `.claude/commands`, instructions from `CLAUDE.md`, `AGENTS.md`,
  `copilot-instructions.md` and `*.instructions.md`, and MCP from `.mcp.json` /
  `.github/mcp.json` (trusted folders only).
- **Copilot agent tools**: built-in aliases `execute` (≙ `Bash`, `shell`, `powershell`),
  `read` (≙ `Read`), `edit` (≙ `Edit`, `Write`, `MultiEdit`), `search` (≙ `Grep`, `Glob`),
  `agent` (≙ `Task`), `web` (≙ `WebFetch`, `WebSearch`), `todo`. MCP tools are
  `server/tool` or `server/*`. **Unknown names are silently ignored** — by both products —
  which is why drift goes unnoticed: an agent just quietly loses a tool.
- **Claude Code agent tools**: built-ins by name (`Read`, `Bash`, …) and MCP tools as
  `mcp__<server>__<tool>`, where `<server>` is the key in `.mcp.json`. A tool installed
  through a personal plugin has a different prefix (`mcp__plugin_<plugin>_<server>__…`)
  and does not exist for teammates.

## Workflow

### 1. Edit the canonical source

| You want to… | Edit | Then |
| --- | --- | --- |
| Add/change an MCP server | `.mcp.json` | run the script (`--fix` regenerates `src/.mcp.json` and plugin entries) |
| Add/change an agent | `.claude/agents/<name>.md` | check its `tools:` line (step 3); `--fix` generates `.github/agents/<name>.agent.md`; sync the plugin copy if bundled |
| Add/change a skill | `.claude/skills/<name>/SKILL.md` | `name:` must equal the folder name; `--fix` copies it to `.github/skills/<name>/`; sync the plugin copy if bundled |
| Add/change a slash command | `.claude/commands/<ns>/<name>.md` | `--fix` regenerates `.github/prompts/<ns>-<name>.prompt.md` |
| Change repo-wide rules | `CLAUDE.md` | port the change into `.github/copilot-instructions.md` by hand (step 4) |

If someone edited a **mirror** directly (a `.github/prompts/*.prompt.md`, a
`.github/skills/*` twin, a `.github/agents/*.agent.md`, a plugin copy), port that edit back into the canonical file
**first** — `--fix` overwrites mirrors from the canonical side and would discard it.
Check with `git diff` before fixing.

Do not add tooling machine-locally (`claude mcp add`, `copilot mcp add`, user-level
agents/skills): teammates would not get it.

### 2. Run the drift check

From the repo root:

```bash
node .claude/skills/ai-tooling-sync/scripts/sync-ai-tooling.mjs          # report; exits 1 on errors
node .claude/skills/ai-tooling-sync/scripts/sync-ai-tooling.mjs --fix    # regenerate mechanical mirrors, then re-check
```

`--fix` handles: command → prompt mirrors, `src/.mcp.json`, plugin MCP entries, a
`.github/skills/<name>/` copy of **every** skill, a generated `.github/agents/<name>.agent.md`
for **every** agent (Copilot tool names only — `mcp__…` entries dropped, Claude built-ins
mapped to `read`/`edit`/`search`/`execute`/… — and no `model:`, since Claude's model
aliases mean nothing to Copilot), and re-copying existing plugin twins. It never deletes a
Copilot-only file, creates new plugin twins, edits canonical agents, bumps versions or
touches instructions — those need judgement and are reported as findings.

The script has its own tests — run them after changing it:
`node --test .claude/skills/ai-tooling-sync/scripts/sync-ai-tooling.test.mjs`.

`✗` errors break a product for someone and must be fixed. `!` warnings are gaps worth
closing (usually an asset missing from an instruction file).

### 3. Agents — the `tools:` line

Both products read the same `.claude/agents/*.md` file, so its `tools:` line must
speak **both vocabularies**:

```yaml
tools: Read, Write, Edit, Glob, Grep, Bash, mcp__primeng__get_component, mcp__primeng__search, read, edit, search, execute, primeng/*
```

- Claude built-ins (`Read`, `Edit`, `Bash`, …) are understood by Copilot too, but add the
  Copilot aliases (`read`, `edit`, `search`, `execute`) anyway — explicit is cheap.
- For every MCP server the agent uses, list **both** `mcp__<server>__<tool>` entries
  (Claude) **and** `<server>/*` (Copilot). The script checks the pairing and that the
  server exists in `.mcp.json`.
- **The script cannot know which tools a server actually exposes.** Verify every
  `mcp__<server>__<tool>` name against the live server: in Claude Code, the deferred
  tool list / `ToolSearch` with `+<server>`; in Copilot CLI, `/mcp`. A misspelled or
  retired tool name is silently dropped — the agent just loses that tool.
- Omitting `tools:` entirely grants all tools in both products; prefer an explicit list.

### 4. Instructions — `CLAUDE.md` ⇄ `.github/copilot-instructions.md`

`CLAUDE.md` is canonical (Claude Code and Copilot CLI read it). The Copilot file is a
**condensed** mirror for Copilot in the IDE, so it is written by hand, not copied:

- Every rule (MUST/never/always) in `CLAUDE.md` has an equivalent in the mirror, in
  fewer words. Prose, tables and rationale may be shortened; rules may not be dropped.
- Both files name every agent, every non-OpenSpec skill and every MCP server — the
  script warns when one is missing.
- When you change one, change the other in the same commit.

### 5. The distributable plugin

`plugins/4dotnet-boogabooster/` bundles a **subset** of `.claude/` for other projects.

- A bundled agent/skill is a byte-for-byte copy of its `.claude/` twin (`--fix` re-copies).
- `.claude-plugin/plugin.json` lists every bundled agent and skill by path.
- Its `.mcp.json` entries match the root definition of the same server.
- **Any content change bumps `version`** in `plugin.json` (patch: fixes/wording;
  minor: new or removed agent/skill/server; major: breaking rename). The script fails
  when content changed but the version did not.
- Adding a new asset to the plugin is a decision, not a sync: only repo-agnostic,
  4dotnet-wide tooling belongs there. Repo-specific skills (OpenSpec, git workflow,
  this one) stay out.

### 6. Finish

1. The script reports `AI tooling is in sync.` (or only warnings you have consciously
   accepted — say which and why).
2. If the asset is user-facing, update the *Working with AI in this repo* section of
   `README.md` too.
3. Remember the CI review flags every change to `.claude/`, `CLAUDE.md` or
   `.github/code-review/` at `major` — mention in the PR description that this is a
   tooling-sync change.

## Keeping this skill true

The product facts above drift faster than the code. When a finding surprises you, or
a product release mentions agents/skills/MCP/prompt files, re-verify against:

- VS Code — `code.visualstudio.com/docs/agents/reference/custom-agent-file-reference`,
  `…/docs/agent-customization/agent-skills`, `…/docs/agent-customization/prompt-files`,
  `…/docs/agents/reference/mcp-configuration`, `…/docs/agents/run/agent-harnesses`
- Copilot custom agents — `docs.github.com/en/copilot/reference/custom-agents-configuration`;
  CLI — `…/copilot/reference/copilot-cli-reference/cli-command-reference`; cloud agent
  skills/MCP — `…/copilot/how-tos/use-copilot-agents/cloud-agent/`
- Visual Studio — `learn.microsoft.com/visualstudio/ide/` `mcp-servers`,
  `copilot-specialized-agents`, `copilot-agent-skills` (the `microsoft-learn` MCP
  server can fetch them)
- Claude Code — `code.claude.com/docs` (sub-agents, skills, slash commands, MCP)

If a product starts reading a canonical source natively (e.g. VS Code learns
`.claude/commands`), retire the mirror: delete it, drop its check from the script,
and update this table, `CLAUDE.md`, `.github/copilot-instructions.md` and `README.md`.
