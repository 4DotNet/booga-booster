# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Status

This repository is an **early-stage scaffold**. Most of the .NET class libraries (the `Controller`, `DigitalTwin`, `Queue`, `Weather` modules) contain only a placeholder `Class1.cs`, the API is still the default Aspire/minimal-API weather-forecast template, and the Angular app has empty routes. Expect to be building features from near-zero rather than modifying established code.

The backend follows the **Modular Monolith** layout mandated by the `4dotnet-csharp-style-guide` MCP server (ADR-0004): each bounded context is a folder containing a module project plus an `.Abstractions` project, shared code lives under `Shared/`, and a single root API (`FourDotnet.BoogaBooster.Api`) hosts every module.

## Architecture

Two stacks live under `src/`, wired together by .NET Aspire.

### .NET backend (`.NET 10`)

- **`Aspire/FourDotnet.BoogaBooster.Aspire.AppHost`** — the Aspire orchestrator and the run/debug entry point for the whole backend. `AppHost.cs` declares the distributed application graph; new services/resources are registered here via `builder.AddProject<>(...)`. Uses `Aspire.AppHost.Sdk`.
- **`Aspire/FourDotnet.BoogaBooster.Aspire.ServiceDefaults`** — shared cross-cutting config (OpenTelemetry, health checks, service discovery, resilience). Every service project references this and calls `builder.AddServiceDefaults()` + `app.MapDefaultEndpoints()` (see `Controller.Api/Program.cs`).
- **`FourDotnet.BoogaBooster.Api`** — the ASP.NET Core minimal-API web service and single root API for the modular monolith (hosts every module); the only project registered in the AppHost graph.
- **`Controller/`, `DigitalTwin/`, `Queue/`, `Weather/`** — one bounded-context module per folder. Each holds a module project (`FourDotnet.BoogaBooster.<Module>`, empty scaffolds intended to hold domain logic) and its public-contract project (`FourDotnet.BoogaBooster.<Module>.Abstractions`). Other modules may reference the `.Abstractions` project **only**, never the module project.
- **`Shared/`** — cross-cutting shared libraries (currently `FourDotnet.BoogaBooster.Core`, the intended home for the DDD domain-model base classes per ADR-0003). Must not hold module-specific logic.

The solution file is `src/BoogaBooster.slnx` (the new XML `.slnx` format).

### Angular frontend (`Angular 21`)

- **`FourDotnet.BoogaBooster.App`** — standalone Angular app (Vitest for tests, Prettier configured). It is **not** currently part of the Aspire graph; run it separately.

## C# conventions

**IMPORTANT: When writing, modifying, or designing any C# (csharp) code, you MUST ALWAYS consult the `4dotnet-csharp-style-guide` MCP server FIRST.** This is not optional. Before producing C# code, changing project/folder structure, or making any solution-design decision, query that server for the applicable guidance and follow it.

- Use `list_documents` to see all available ADRs and guidelines, `search_documents` to find rules by keyword (e.g. `testing`, `minimal-api`, `ddd`), and `get_document` to read a document's full content.
- The server is authoritative on: target framework, API style (minimal APIs vs. controllers), domain modeling (DDD, value objects, entity state), solution/module/folder structure, unit testing tooling, and other C# standards.
- If a rule from the style guide conflicts with existing code in this repo, follow the style guide and flag the discrepancy.

## Common commands

Run all .NET commands from `src/` (where `BoogaBooster.slnx` lives).

```bash
# Run the full backend via Aspire (starts the Aspire dashboard + all registered services)
dotnet run --project Aspire/FourDotnet.BoogaBooster.Aspire.AppHost

# Build / test the whole solution
dotnet build BoogaBooster.slnx
dotnet test BoogaBooster.slnx
```

Angular commands run from `src/FourDotnet.BoogaBooster.App` (package manager: **npm 11**):

```bash
npm start          # ng serve (dev server)
npm run build      # ng build
npm test           # vitest via ng test
npx vitest run path/to/file.spec.ts   # run a single test file
```

## Angular conventions

Detailed frontend rules live in `src/FourDotnet.BoogaBooster.App/.claude/CLAUDE.md` and are authoritative when working in the Angular app. Key points: standalone components (do **not** set `standalone: true` — it is the v20+ default), signals for state (`signal`/`computed`/`update`/`set`, never `mutate`), `input()`/`output()` functions over decorators, `inject()` over constructor injection, `OnPush` change detection, native control flow (`@if`/`@for`/`@switch`), and host bindings in the `host` object rather than `@HostBinding`/`@HostListener`. Accessibility must pass AXE / WCAG AA.

## AI tooling (Claude Code + GitHub Copilot CLI)

This repo is set up so **Claude Code and GitHub Copilot CLI share the same MCP servers, agents and skills**. Copilot CLI natively reads Claude's directories, so `.claude/` is the single source of truth for almost everything:

| Asset | Lives in | Claude Code | Copilot CLI |
| --- | --- | --- | --- |
| MCP servers | `.mcp.json` (repo root) | project scope | workspace scope |
| Agents | `.claude/agents/*.md` | ✅ | ✅ |
| Skills | `.claude/skills/<name>/SKILL.md` | ✅ | ✅ (also scans `.github/skills/`) |
| Instructions | `CLAUDE.md` | ✅ | ✅ |
| Slash commands | `.claude/commands/` **and** `.github/prompts/*.prompt.md` | `.claude/commands/` only | `.github/prompts/` only |

**Rules when adding tooling:**

- **MCP server** → add it to the root `.mcp.json`. Do not add it machine-locally (`claude mcp add`, `copilot mcp add`), or teammates won't get it.
- **Agent or skill** → put it under `.claude/`. Both tools pick it up. If it is part of the distributable plugin, also sync it into `plugins/4dotnet-boogabooster/` and bump the plugin version.
- **Slash command** → this is the only asset that must be written twice: `.claude/commands/<ns>/<name>.md` for Claude and `.github/prompts/<ns>-<name>.prompt.md` for Copilot. Keep the two in sync.
- **Agent `tools:` lists** → the two tools use different tool vocabularies (`Read`/`Bash`/`mcp__server__tool` vs. `read`/`shell`/`server/*`). Unknown names are ignored by both, so list **both** vocabularies on the `tools:` line; otherwise the agent ends up with no tools in one of them.

**First run of Copilot CLI in this repo:** start it interactively once and trust the folder. Workspace configuration (`.mcp.json`) is only loaded for trusted folders — until then `copilot mcp list` shows user-scoped servers only.

The `plugins/4dotnet-boogabooster/` plugin is dual-format: `.claude-plugin/plugin.json` is read by both `claude plugin` and `copilot plugin`.
