# BoogaBooster — Copilot instructions

> **This file is a condensed mirror of [`CLAUDE.md`](../CLAUDE.md), which is canonical.**
> It exists because GitHub Copilot in the IDE (VS Code / Visual Studio) does not read
> `CLAUDE.md`, while Claude Code and Copilot CLI do. **Change one, change both.** For
> anything not covered here, read `CLAUDE.md`.

## What this project is

BoogaBooster is a **digital twin of a carnival ride**, and simultaneously a demo project
for teaching AI-assisted development. Both halves matter: the code is held to production
standards, and the agents/skills/MCP servers/specs in the repo are part of the
deliverable. `README.md` has the full tour.

The ride: one motor-driven **Great Mill** (four 6 m arms) carrying four motor-driven
**Hubs** (four 2 m arms each), each hub arm tip carrying a passively free-spinning
**Gondola** — 16 gondolas × 2 seats = 32 seats. Five driven axes, 16 free axes. Physics
runs on a fixed 1/120 s timestep and is **deterministic**; telemetry publishes at 30 Hz.

## Non-negotiable rules

1. **Before writing, changing or designing any C#, consult the
   `4dotnet-csharp-style-guide` MCP server first.** It is authoritative on target
   framework, API style, DDD/value objects, solution and folder structure, and testing
   tooling. If it conflicts with existing code, follow the guide and flag the
   discrepancy.
2. **Modules reference other modules' `.Abstractions` project only** — never another
   module project.
3. **No endpoint mappings in `Api/Program.cs`.** Endpoints belong to the owning module's
   `Endpoints/` class; `Program.cs` only composes.
4. **No business logic in endpoints.** Parse the request into a command/query and
   dispatch to an `ICommandHandler<T>` / `IQueryHandler<T,R>`.
5. **No magic numbers in the physics.** Every constant lives in
   `DigitalTwin/Domain/RideParameters.cs`, derived in `docs/`.
6. **Determinism is a hard requirement** in the simulation. Randomness goes through an
   injected sampler; time goes through `TimeProvider`.
7. **Accessibility must pass AXE / WCAG AA** in the Angular app.
8. Module and `Shared/` libraries must hold **≥ 80 % line coverage**.

## Repository layout

```
docs/            physics & simulation derivations — the spec for the maths
openspec/        spec-driven workflow: specs/ (contract) + changes/ (in flight)
src/
  BoogaBooster.slnx              solution (new XML .slnx format)
  Aspire/                        AppHost (run entry point) + ServiceDefaults
  FourDotnet.BoogaBooster.Api    single root API — composition root only
  DigitalTwin/                   the ride: physics, state machine, telemetry
  Queue/                         guests, groups, arrival simulation
  Weather/                       autonomous weather world
  Controller/                    empty scaffold (ride control/programmes)
  Shared/                        Core (DDD + CQRS base) + IntegrationMessages
  Tests/                         one xUnit project per module
  FourDotnet.BoogaBooster.App    Angular 22 operator dashboard
```

## Status

`DigitalTwin`, `Queue`, `Weather`, both `Shared/` libraries, the API and the Angular app
are **built and tested** (~330 xUnit v3 tests, 23 Vitest spec files). `Controller/` is
an **empty scaffold** — two `.csproj` files and nothing else.

## Backend — .NET 10 modular monolith

Each bounded context is a folder with two projects: `FourDotnet.BoogaBooster.<Module>`
(domain + application + features + endpoints) and
`FourDotnet.BoogaBooster.<Module>.Abstractions` (public contract). Module anatomy:

```
<Module>ModuleExtensions.cs    Add<Module>Module() - DI registration
<Module>ModuleOptions.cs       bound options, SectionName const
Domain/                        aggregates, value objects, domain services
Application/                   application services, stores, background services
Features/<FeatureName>/        <Feature>Command|Query + its handler
Endpoints/                     Map<Module>Endpoints() - HTTP surface
```

CQRS abstractions come from `FourDotnet.BoogaBooster.Core.Cqrs`; DDD base classes
(`DomainModel`, `DomainModelState`, `DomainValidationException`) from
`FourDotnet.BoogaBooster.Core`. Broken invariants throw `DomainValidationException`,
which endpoints translate to `400`.

**Orchestration.** `Aspire/…AppHost` is the run entry point and declares the whole
graph: a RabbitMQ container, a **programmatically declared** Dapr `pubsub` component
(Aspire Community Toolkit — there are no `components/*.yaml` files), the API with its
Dapr sidecar, and the Angular app as a Vite resource.

**Messaging.** Publish by injecting `IIntegrationEventPublisher`. Declare events in
`Shared/IntegrationMessages/Events/<Module>/` with `[TopicName]`. Subscribe with a
minimal-API endpoint annotated `.WithTopic(...)`, discovered via
`MapSubscribeHandler()` — no subscription YAML. Read
`src/Shared/FourDotnet.BoogaBooster.IntegrationMessages/README.md` before adding an
event. Current flow: Weather publishes `weather-updated` → Queue scales its arrival
rate; Queue publishes `group-queued`.

## Frontend — Angular 22

Standalone, **zoneless** (no zone.js polyfill), signal-first. Vitest + `axe-core`;
Three.js for the 3D visualization. Part of the Aspire graph via `AddViteApp`, so the
AppHost starts it; `npm start` runs it standalone.

Detailed rules live in `src/FourDotnet.BoogaBooster.App/.claude/CLAUDE.md` and are
authoritative in the app. Key points: do **not** set `standalone: true` (v20+ default);
signals for state (`signal`/`computed`/`update`/`set`, never `mutate`);
`input()`/`output()` functions over decorators; `inject()` over constructor injection;
`OnPush`; native control flow (`@if`/`@for`/`@switch`); host bindings in the `host`
object, not `@HostBinding`/`@HostListener`.

**PrimeNG 22** + `@primeuix/themes` are installed and are the mandated component
library, but **no component uses them yet** — the existing panels are hand-rolled. New
UI work should adopt PrimeNG (consult the `primeng` MCP server first).

Telemetry arrives over **SSE** (`GET /ride/telemetry/stream`); queue and weather are
polled HTTP. In dev, `/api/*` is proxied to the API by `proxy.conf.js`.

## Physics and the ride domain

`docs/` holds the derivations for the whole simulation — coordinate frames, the
fixed-timestep tick, `τ = Iα` and the symplectic integrator, the motor torque curve and
loss model, the passive gondola pendulums, the G-force and imbalance maths. **It is the
specification the physics code and its tests are written against** — read the relevant
doc before changing anything in `DigitalTwin/Domain`.
`docs/appendix-parameters.md` carries the baseline values and a sanity check.

**Terminology:** `docs/` says *cart*, the code says **gondola**. Same thing; use
*gondola* in code.

## Testing

- Backend: **xUnit v3** + **Moq** + **Bogus** +
  `Microsoft.Extensions.TimeProvider.Testing`. **No FluentAssertions.** One test project
  per module under `src/Tests/`.
- Frontend: **Vitest**, plus `axe-core` accessibility specs (`*.a11y.spec.ts`).

## Spec-driven workflow (OpenSpec)

Features go through `openspec/` rather than straight into code: explore → propose
(`proposal.md`, `design.md`, `specs/*/spec.md`, `tasks.md`) → apply → archive.

`openspec/specs/` holds the 20 published capabilities — **treat them as the current
behavioural contract** and read the relevant one before changing a feature.
`openspec/changes/` holds work in flight: `passenger-safety` (over-speed protection) and
`single-riders-queue` are proposed but **not yet implemented**.

Copilot CLI runs the workflow through the prompts in `.github/prompts/`
(`opsx-explore`, `opsx-propose`, `opsx-apply`, `opsx-archive`).

## Known style-guide discrepancies

Do not copy these; fix them when you touch the surrounding code.

- **DTO placement.** The mandated layout is
  `<Module>.Abstractions/DataTransferObjects/<Feature>/` holding `<Feature>Request` and
  `<Feature>Response`. Today the Queue module's DTOs (`PersonDto`, `QueuedGroupDto`,
  `QueueStatusDto`) sit at the Abstractions project root, and the DigitalTwin/Weather
  request records are declared inline inside their `Endpoints` classes.

## Common commands

```bash
cd src
dotnet run --project Aspire/FourDotnet.BoogaBooster.Aspire.AppHost   # run everything
dotnet build BoogaBooster.slnx
dotnet test  BoogaBooster.slnx
```

The AppHost needs **Docker** (RabbitMQ container), an initialised **Dapr CLI**
(`dapr init`), and RabbitMQ credentials as secret Aspire parameters:

```bash
dotnet user-secrets --project Aspire/FourDotnet.BoogaBooster.Aspire.AppHost \
  set Parameters:rabbitmq-username guest
dotnet user-secrets --project Aspire/FourDotnet.BoogaBooster.Aspire.AppHost \
  set Parameters:rabbitmq-password guest
```

Angular (from `src/FourDotnet.BoogaBooster.App`, package manager **npm 11**):

```bash
npm start                              # ng serve
npm run build
npm test                               # vitest via ng test
npx vitest run path/to/file.spec.ts    # single file
```

## MCP servers

Configured in the root `.mcp.json` (workspace scope). **First run of Copilot CLI in this
repo: start it interactively once and trust the folder**, or `.mcp.json` is never loaded
and `copilot mcp list` shows user-scoped servers only.

| Server | Use it for |
| --- | --- |
| `4dotnet-csharp-style-guide` | **Authoritative** for all C# and solution-design decisions. Query first, every time. Needs the `4dotnet-csharp-style-guide` executable on `PATH`. |
| `primeng` | PrimeNG 22 component API, props, events, theming, accessibility. |
| `microsoft-learn` | Official Microsoft/Azure docs and code samples. |

## Adding tooling

- **MCP server** → root `.mcp.json`. Never machine-local (`copilot mcp add`), or
  teammates won't get it.
- **Agent or skill** → `.claude/` (Copilot CLI reads it natively; it also scans
  `.github/skills/`). If it belongs in the distributable plugin, sync it into
  `plugins/4dotnet-boogabooster/` and bump the plugin version.
- **Slash command** → written twice: `.claude/commands/<ns>/<name>.md` **and**
  `.github/prompts/<ns>-<name>.prompt.md`. Keep them in sync.
- **Repo-wide instructions** → written twice: `CLAUDE.md` (canonical) **and** this file.
  Keep them in sync.
- **Agent `tools:` lists** → list **both** tool vocabularies (`Read`/`Bash`/
  `mcp__server__tool` *and* `read`/`shell`/`server/*`). Unknown names are ignored by
  both tools; omitting one leaves the agent tool-less in that tool.
