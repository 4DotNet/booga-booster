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
