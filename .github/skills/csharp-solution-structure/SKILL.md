---
name: csharp-solution-structure
description: >-
  Mandatory .NET solution, module and project layout (ADR-0001, ADR-0004,
  ADR-0008). Use whenever adding, renaming or moving a .NET project, creating a
  new module or bounded context, editing a .csproj or .slnx, setting a target
  framework, deciding which project a type belongs in, adding a project
  reference, or reviewing cross-module coupling. Every bounded context is
  exactly two projects (the module and its .Abstractions); other modules
  reference the .Abstractions project ONLY; everything lives under src/.
---

# C# Solution and Module Structure

The layout below is **mandatory** — ADR-0004 states no other layout is permitted.
When you find a violation while working on something else, flag it; when a
requested change would create one, put the code in the correct place instead and
say so.

## Non-negotiable

- **`net10.0` in every project.** Never target a lower framework. (`adr-0001-r1`,
  `adr-0001-r2`)
- **The whole solution is rooted under `src/`.** (`adr-0004-r3`)
- **One module per bounded context, exactly two projects**: the module project
  and its `.Abstractions` project, both inside a folder named after the module.
  (`adr-0004-r2`)
- **Cross-module references point at `.Abstractions` only** — never at another
  module's implementation project. (`adr-0004`)
- **This solution is a Modular Monolith**: one root API project hosting every
  module. Never add a per-module API project. (`adr-0004-r1`)
- **`Shared/` holds genuinely cross-cutting plumbing only** — DDD base classes,
  CQRS abstractions, integration messages. Never module-specific logic.
  (`adr-0004-r3`)
- **Aspire projects live in `src/Aspire/`.** (`adr-0008-r2`)
- **Test projects live in `src/Tests/`**, one per module library, named
  `<ModuleLibrary>.Tests`. Never inside the module folder, never shared across
  modules. (`guideline-unit-testing-r4`)

## The layout

```
src/
  BoogaBooster.slnx                          the solution (new .slnx format)
  Aspire/
    FourDotnet.BoogaBooster.Aspire.AppHost/
    FourDotnet.BoogaBooster.Aspire.ServiceDefaults/
  <Module>/                                  one folder per bounded context
    FourDotnet.BoogaBooster.<Module>/            implementation
    FourDotnet.BoogaBooster.<Module>.Abstractions/   public contract
  Shared/
    FourDotnet.BoogaBooster.Core/            DDD + CQRS base classes
    FourDotnet.BoogaBooster.IntegrationMessages/
  Tests/
    FourDotnet.BoogaBooster.<Module>.Tests/
  FourDotnet.BoogaBooster.Api/               single root API host
```

Existing modules: `Controller`, `DigitalTwin`, `Queue`, `Weather`.

## What goes in which project

| Kind of type | Project |
| --- | --- |
| Aggregates, value objects, domain services | module → `Domain/` |
| Application services, stores, background services | module → `Application/` |
| Commands, queries, handlers | module → `Features/<Feature>/` |
| Endpoint mappings | module → `Endpoints/` |
| Public interfaces other modules consume | `.Abstractions` |
| Request/Response DTOs | `.Abstractions/DataTransferObjects/<Feature>/` — see the `dto-organization` skill |
| Integration event definitions | `Shared/…IntegrationMessages/Events/<Module>/` |
| DDD / CQRS base classes | `Shared/…Core/` |

## Violations to catch

- A module without an `.Abstractions` project, or with an API project of its own.
- A `ProjectReference` from one module to another module's **implementation**
  project.
- Module-specific logic that has drifted into `Shared/`.
- A new project not added to `BoogaBooster.slnx`.
- A `<TargetFramework>` below `net10.0`, or a project missing the element.
- A test project inside a module folder instead of `src/Tests/`.

## Read further

For rationale, the microservices variant, or a case the rules above do not
settle, read the source ADRs from the `4dotnet-csharp-style-guide` MCP server:
`get_document` with `adr-0004` (structure), `adr-0001` (target framework),
`adr-0008` (Aspire folder).
