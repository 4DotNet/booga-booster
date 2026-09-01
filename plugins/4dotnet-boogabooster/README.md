# 4DotNet BoogaBooster Toolkit

Plugin bundling the development tooling for BoogaBooster-style projects. Works with
**both Claude Code and GitHub Copilot CLI** — Copilot CLI reads the same
`.claude-plugin/plugin.json` manifest.

- **Agents**
  - `angular-architect` — zoneless, signal-first, PrimeNG-based Angular work
  - `csharp-expert` — C# / .NET backend work, ADR-compliant, style-guide driven
- **Skills**
  - `dto-organization` — enforces DTO placement in module Abstractions projects
  - `test-coverage` — maintains ≥80% line coverage on module and shared libraries
- **MCP server**
  - `4dotnet-csharp-style-guide` — authoritative C# style guide and ADRs

## Prerequisite

The MCP server runs the `4dotnet-csharp-style-guide` executable, which must be
installed and on `PATH` before the plugin is enabled.

## Install

Claude Code:

```
/plugin marketplace add 4dotnet/booga-booster
/plugin install 4dotnet-boogabooster@booga-booster
```

GitHub Copilot CLI:

```
copilot plugin install 4dotnet/booga-booster:plugins/4dotnet-boogabooster
```

## Local development

```bash
claude --plugin-dir ./plugins/4dotnet-boogabooster
claude plugin validate ./plugins/4dotnet-boogabooster

copilot --plugin-dir ./plugins/4dotnet-boogabooster
copilot plugin list --plugin-dir ./plugins/4dotnet-boogabooster
```

## Notes on cross-tool compatibility

- Agent `tools:` lists carry **both** tool vocabularies (Claude's `Read`/`Bash`/
  `mcp__server__tool` and Copilot's `read`/`shell`/`server/*`). Each tool ignores
  the names it does not recognise; dropping one vocabulary would leave the agent
  with no tools in that tool.
- `plugin.json` declares `agents`, `skills` and `mcpServers` as explicit paths so
  both CLIs resolve the components the same way.
