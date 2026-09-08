# BoogaBooster

A **digital twin of a carnival ride**, built as a hands-on playground for learning
**AI-assisted software development**.

The code is real: a .NET 10 modular monolith running a deterministic physics
simulation, a Dapr/RabbitMQ event bus, a Server-Sent-Events telemetry stream, and an
Angular 22 operator dashboard — all orchestrated by .NET Aspire. But the *point* of
the repository is the tooling around it. Every agent, skill, MCP server and spec in
here exists so you can practise building features **with** an AI assistant instead of
next to one.

> **New here?** Read [Working with AI in this repo](#working-with-ai-in-this-repo),
> then pick something from [Feature ideas to build](#feature-ideas-to-build) and let
> the agents do the driving.

---

## The ride

BoogaBooster is a three-level nested rotating rig spinning about a single vertical axis:

```
              Great Mill  (1 driven spindle, four 6 m arms)
                   |
        +----------+----------+
      Hub 1      Hub 2  ...  Hub 4    (4 driven spindles, four 2 m arms each)
        |
   +----+----+
  G1   G2 ... G4                      (4 gondolas per hub - 16 total, 2 seats each)
```

- **5 driven axes** (1 mill + 4 hubs) and **16 free axes** — the gondolas have no
  motor, they free-spin on an offset pivot and swing passively under the centrifugal
  field. 21 rotating bodies, **32 seats**.
- The physics runs at a **fixed 1/120 s timestep** and is fully deterministic;
  telemetry is published at **30 Hz**.
- Guests arrive in **groups**, wait in a **queue**, board whole gondolas, pull their
  restraints down after a few seconds, and the ride refuses to start until every
  interlock clears (restraints secured, load balanced, under the 3200 kg limit).
- The **weather** simulates itself and drives how fast the queue fills. Nice weather →
  crowds. A storm → a trickle.

The full derivations — torque curves, the symplectic integrator, load-dependent
inertia, the G-force beat — live in **[`docs/`](docs/)**. That folder is the ground
truth the tests are written against; read it before touching the physics.

> **Terminology note:** `docs/` calls the passenger vehicles *carts*; the code calls
> them **gondolas**. Same thing.

---

## Quick start

### Prerequisites

| Requirement | Why |
| --- | --- |
| **.NET 10 SDK** | the backend targets `net10.0` |
| **Node 22+ / npm 11** | the Angular app pins `npm@11.5.2` |
| **Docker** (or Podman) | Aspire starts RabbitMQ in a container |
| **Dapr CLI**, initialised (`dapr init`) | the API runs with a Dapr sidecar for pub/sub |
| `4dotnet-csharp-style-guide` on `PATH` | required by the MCP server the AI tooling depends on |

### Run everything

```bash
cd src

# RabbitMQ credentials are secret Aspire parameters - set them once
dotnet user-secrets --project Aspire/FourDotnet.BoogaBooster.Aspire.AppHost \
  set Parameters:rabbitmq-username guest
dotnet user-secrets --project Aspire/FourDotnet.BoogaBooster.Aspire.AppHost \
  set Parameters:rabbitmq-password guest

dotnet run --project Aspire/FourDotnet.BoogaBooster.Aspire.AppHost
```

The AppHost is the **single entry point**. It brings up RabbitMQ (plus the management
plugin), programmatically declares the Dapr `pubsub` component, starts the API with
its Dapr sidecar, and runs the Angular dev server as a Vite resource. Open the
**Aspire dashboard** link it prints, then follow the `frontend` endpoint to the
operator dashboard.

### Build and test

```bash
cd src
dotnet build BoogaBooster.slnx
dotnet test  BoogaBooster.slnx          # ~330 xUnit v3 tests

cd FourDotnet.BoogaBooster.App
npm test                                # Vitest, 23 spec files (incl. AXE a11y specs)
npx vitest run src/app/queue/state/queue-state.service.spec.ts   # single file
```

---

## Automated code review (CI)

Every pull request into `main` gets reviewed by **GitHub Copilot CLI running headless**,
grounded in this repository's own checked-in standards. The workflow is
`.github/workflows/code-quality-check.yml`.

> **A green `code-quality-check` is not a green build.** This workflow reviews the diff.
> It does **not** compile the code, run the tests or check coverage — there is no
> build/test workflow in this repo yet. Do not read the check as a substitute for
> `dotnet test`.

### What it does

1. Computes the PR's changed files and unified diff from the merge base, onto disk. Once,
   in a `prepare` job, so every reviewer judges byte-identical input.
2. Runs `copilot -p` with `.github/code-review/review-prompt.md` **once per model**, as
   separate matrix legs — currently `claude-sonnet-5` and `gpt-5.6-terra`. The prompt
   tells each to review only the changed lines and to ground every finding in a specific
   rule file (`CLAUDE.md`, a `.claude/skills/*` rule, an `openspec/specs/` requirement,
   `docs/`). Neither reviewer can see the other's output.
3. Each reviewer writes `.code-review/findings.json` — path, line, severity, category,
   rationale and the standard cited.
4. `.github/code-review/compare-reviews.mjs` pairs the reviews: findings on the same file
   within a few lines, with the same category or an overlapping title, become one problem.
   It reports per-reviewer counts, what they agreed on, what each found alone, where they
   graded the same problem differently, and an agreement rate.
5. A **third Copilot session** (`.github/code-review/compare-prompt.md`) reads that report,
   both raw findings files and the diff, and writes the narrative: which single-reviewer
   findings are real, who graded a disagreement correctly, which review was more useful.
   It is commentary — it cannot change a severity or the check result.
6. `.github/code-review/publish-review.mjs` posts the merged findings as **inline review
   comments** on the changed lines, each naming the reviewers that reported it, plus a
   summary carrying the comparison. Findings that cannot be anchored to a diff line go in
   the summary rather than being dropped.
7. The check **fails only on `blocking` findings**, taking the **union** of the reviewers:
   a problem inherits the worst severity any model gave it, so one model catching a MUST
   violation alone still fails the check. `major`, `minor` and `nit` are advisory.

Drafts and pull requests **from forks are skipped** with a passing check — the
`pull_request` event grants forks no secrets, so the review cannot run for them.

### One-time setup

Add a repository (or organisation) secret named **`COPILOT_GITHUB_TOKEN`** holding a
personal access token for an identity with an **active Copilot seat**.

The Actions-provided `GITHUB_TOKEN` **cannot** be used: it is an installation token with
no Copilot entitlement. `COPILOT_GITHUB_TOKEN` is also the variable the CLI itself reads,
and it takes precedence over `GH_TOKEN` and `GITHUB_TOKEN`.

**Use a dedicated machine account, not your own PAT.** With a personal token:

- every PR review spends *your* AI-credit allowance, i.e. your working capacity;
- the token carries *your* access to every repo you can reach into CI;
- the gate breaks silently when you rotate it, change teams or leave.

A machine account with its own Copilot seat and a fine-grained PAT scoped to this
repository (read-only contents is enough — the reviewer never writes to GitHub) avoids
all three.

Also required:

- **Copilot policy** — for Copilot Business/Enterprise, the **"Copilot in the CLI"**
  organisation policy must be enabled, and the pinned model must be permitted.
- **Node 22** — set by the workflow.

### Cost

Reviews draw **AI credits** from the seat's allowance, not metered API tokens.

- A run starts **three sessions** — two reviews and the comparison — so it costs roughly
  three times a single-reviewer run.
- `MAX_AI_CREDITS` in the workflow is a **hard per-session cap** (`--max-ai-credits`), so
  the ceiling for a whole run is three times that number.
- Dropping the second reviewer is a one-line matrix edit; dropping the narrative is one
  step. Both are in `.github/workflows/code-quality-check.yml`.
- Each run's actual usage appears in the **Actions job summary**, per session, read from
  the CLI's `--usage-output-file`.
- Consumption is bounded further by per-PR concurrency cancellation (a new push cancels
  the superseded run), the draft skip, `paths-ignore` for image assets, and a job timeout.

### Security posture

The workflow is split into two jobs on purpose:

| Job | Credential | Permissions | Runs the model |
| --- | --- | --- | :-: |
| `prepare` | — | `contents: read` | — |
| `review` (one leg per model) | `COPILOT_GITHUB_TOKEN` (user PAT) | `contents: read` | ✅ |
| `compare` | `COPILOT_GITHUB_TOKEN` (user PAT) | `contents: read` | ✅ |
| `publish` | Actions `GITHUB_TOKEN` | `contents: read`, `pull-requests: write` | — |

So no job holding a user PAT can write to the PR, and the job that can write to the PR
never runs a model. `compare` checks out the **base branch** rather than the PR head, so
the standards its judgement rests on are versions the pull request cannot have edited. Comments are attributed to the Actions bot, not to the seat
owner. Neither job gets `contents: write`.

The comparison session is constrained further, because it reads two other models’ output:
the document the check is computed from is kept **outside the working directory** while it
runs, and every file it *can* reach is hashed before and verified after, so it cannot
quietly rewrite a reviewer’s findings to change the outcome. The publisher also refuses a
merged findings document whose finding count the comparison does not corroborate.

Each reviewer runs with the `shell` and `url` tool kinds **denied**, built-in MCP servers
disabled, repo instruction auto-loading off, and file access confined to the checkout —
so no `git`, `gh`, `dotnet`, `npm`, no network, no GitHub API. After it exits, the job
asserts the working tree is unchanged apart from `.code-review/`; that assertion is the
actual read-only guarantee, and it holds regardless of whether the CLI's flags behave as
documented.

### Tuning it

| To change | Edit |
| --- | --- |
| What counts as `blocking` | the severity section of `.github/code-review/review-prompt.md` |
| Which standards are consulted | the standards section of the same file |
| Which models review | the `strategy.matrix.model` list in `.github/workflows/code-quality-check.yml` — the only place the roster is written |
| Which model narrates the comparison | `COMPARE_MODEL` in that workflow's `env:` block |
| How findings are paired across reviewers | the window and threshold constants at the top of `.github/code-review/compare-reviews.mjs` |
| What the comparison judges | `.github/code-review/compare-prompt.md` |
| The credit cap or CLI version | the `env:` block of `.github/workflows/code-quality-check.yml` |
| Whether findings gate the merge, and whether the gate stays a union | the exit condition in `.github/code-review/publish-review.mjs` (see design D15 before relaxing it to consensus) |

Run the publisher's tests with:

```bash
node --test .github/code-review/publish-review.test.mjs .github/code-review/compare-reviews.test.mjs
```

**Bumping the pinned CLI version is a behavioural change, not a chore.** Three of this
workflow's original assumptions about Copilot CLI's flag surface turned out to be wrong
during implementation (see `openspec/changes/code-quality-check/design.md`, D4–D6), so
re-verify the tool restrictions on a probe PR after any bump.

### Troubleshooting

The check failed but you see no review comments — in likely order:

1. **`COPILOT_GITHUB_TOKEN` is missing.** The first step fails loudly and names it.
2. **The Copilot seat is inactive, expired or revoked**, or the token was rotated.
3. **The AI-credit allowance is exhausted.**
4. **The pinned model is blocked by org policy.**
5. **The credit cap was hit mid-review**, so no findings file was written. Raise
   `MAX_AI_CREDITS`.
6. **The findings file was missing or malformed.** This deliberately fails rather than
   reporting a clean review — a review that did not happen must never look like a review
   that found nothing.
7. **One reviewer failed and the other did not.** The `compare` job never starts and
   nothing is published: a comparison of one review must not look like a comparison of
   two. Check both matrix legs, and whether the second model is permitted by policy.
8. **The review published but the comparison narrative is missing.** That one degrades
   rather than fails: look for the warning in the `compare` job. The reviewers' findings
   and the deterministic comparison are unaffected.

Making `code-quality-check` a **required** check on `main` is a deliberate follow-up.
Calibrate the prompt against real pull requests first: an AI gate that fires on taste
gets switched off within a week.

---

## Repository layout

```
.claude/                 agents, skills, slash commands  <- shared with Copilot CLI
.claude-plugin/          marketplace manifest
.github/                 copilot-instructions.md, Copilot-CLI prompts, skill mirrors
.mcp.json                MCP servers (project / workspace scope)
CLAUDE.md                repo-wide instructions for AI assistants (canonical)
designs/3d/              Blender source for the ride model
docs/                    physics & simulation derivations (the spec for the maths)
openspec/                spec-driven change workflow: specs/ + changes/
plugins/                 the distributable 4dotnet-boogabooster plugin
src/
  BoogaBooster.slnx              solution (new XML .slnx format)
  Aspire/                        AppHost + ServiceDefaults
  FourDotnet.BoogaBooster.Api    the single root API hosting every module
  DigitalTwin/                   the ride: physics, state machine, telemetry
  Queue/                         guests, groups, arrival simulation
  Weather/                       autonomous weather world
  Controller/                    (!) empty scaffold - up for grabs
  Shared/                        Core (DDD/CQRS base) + IntegrationMessages
  Tests/                         one xUnit project per module
  FourDotnet.BoogaBooster.App    Angular 22 operator dashboard
```

---

## Architecture

### Modular monolith (ADR-0004 / ADR-0007)

Each bounded context is a folder holding **two** projects:

- `FourDotnet.BoogaBooster.<Module>` — domain, application services, features, endpoints.
- `FourDotnet.BoogaBooster.<Module>.Abstractions` — the public contract.

**Modules may reference other modules' `.Abstractions` only — never the module project
itself.** Cross-module coupling that should stay async goes through integration events
instead.

Every module owns its own HTTP surface (`MapXxxEndpoints()`) and its own DI wiring
(`AddXxxModule()`). `Api/Program.cs` is nothing but composition:

```csharp
builder.AddBoogaBoosterIntegrationMessages();
builder.AddWeatherModule();
builder.AddQueueModule();
builder.AddDigitalTwinModule();
// ...
app.MapWeatherEndpoints();
app.MapQueueEndpoints();
app.MapDigitalTwinEndpoints();
```

Endpoints contain **no business logic** — they parse the request into a command or
query and dispatch it to an `ICommandHandler<T>` / `IQueryHandler<T,R>`. Broken
invariants throw `DomainValidationException` and surface as `400`.

### The modules

| Module | What it does |
| --- | --- |
| **DigitalTwin** | The ride itself. `Ride` → `GreatMill` → `Hub` → `Gondola` → `Seat` aggregates, `RotationalDynamics` / `RideKinematics` physics, the guarded lifecycle state machine (`Idle → Loading → Safe → Started → Stopping → Offloading`, plus `EmergencyStop`), the boarding coordinator, and the 30 Hz telemetry stream. |
| **Queue** | The waiting line. `RideQueue`, `QueuedGroup`, `Person`, an `ArrivalPlanner` / `IntervalPlanner` pair, and a `RideQueueFillerService` background service that invents guests. Oversized groups are split so they can never wait forever. |
| **Weather** | An autonomous weather world: temperature, Beaufort wind, sunshine, precipitation, and a rolled-up `NiceWeather` score in `[0,1]`. Drifts with mean reversion; operators can trigger precipitation and strong-wind events. Publishes `WeatherUpdateIntegrationEvent`. |
| **Controller** | **Still an empty scaffold.** Intended home for the ride's control / operations logic. |
| **Shared/Core** | `DomainModel`, `DomainModelState`, `DomainValidationException`, and the CQRS handler abstractions. |
| **Shared/IntegrationMessages** | The event-bus contract: `IIntegrationEvent`, `[TopicName]`, `TopicNameResolver`, and the Dapr-backed publisher. |

### Messaging

Aspire declares the Dapr pub/sub component **programmatically** (no
`components/*.yaml`) over a RabbitMQ container. Publishers inject
`IIntegrationEventPublisher`; subscribers are plain minimal-API endpoints annotated
with `.WithTopic(...)`, discovered through `MapSubscribeHandler()`. See
`src/Shared/FourDotnet.BoogaBooster.IntegrationMessages/README.md`.

Today's flow: **Weather** publishes `weather-updated` → **Queue** consumes it and
scales its arrival rate; **Queue** publishes `group-queued`.

### Frontend

Angular 22, **zoneless** (no zone.js polyfill), signal-first, Three.js for the 3D
ride, Vitest + `axe-core` for tests. The dashboard is a three-column operator console:
status, queue, controls and weather on the left, the live 3D visualization in the
centre, telemetry panels on the right. Telemetry arrives over **SSE**
(`GET /ride/telemetry/stream`); queue and weather are polled HTTP through the dev
proxy (`/api/*`).

**PrimeNG 22** + `@primeuix/themes` are installed and are the mandated component
library — the `angular-architect` agent consults the `primeng` MCP server before using
one — but no component uses them yet. The existing panels are hand-rolled, so the
first PrimeNG-based feature also gets to set the pattern.

Frontend conventions are authoritative in
[`src/FourDotnet.BoogaBooster.App/.claude/CLAUDE.md`](src/FourDotnet.BoogaBooster.App/.claude/CLAUDE.md).
Highlights: no `standalone: true`, `input()` / `output()` over decorators, `inject()`
over constructors, `OnPush`, native `@if` / `@for`, signals (`set` / `update`, never
`mutate`), and **AXE / WCAG AA must pass**.

---

## HTTP API

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/ride/telemetry` | one full telemetry snapshot |
| `GET` | `/ride/telemetry/stream` | SSE stream of snapshots (30 Hz while running) |
| `POST` | `/ride/state` | request a lifecycle transition |
| `POST` | `/ride/start`, `/ride/stop` | shortcuts onto the same state machine |
| `POST` | `/ride/main-power`, `/ride/hub-power` | throttle 0-100 % |
| `POST` | `/ride/main-direction`, `/ride/hub-direction` | `Forward` / `Reverse` |
| `POST` | `/ride/brake` | engage / release a single gondola brake |
| `POST` | `/ride/engine-brake` | engage / release the engine brake |
| `POST` | `/ride/passengers` | board a passenger into a seat |
| `GET` | `/rides/{rideId:guid}/queue` | the waiting line |
| `GET` | `/weather` | current conditions |
| `POST` | `/weather/precipitation`, `/weather/strong-wind` | trigger a disturbance |

OpenAPI is exposed at `/openapi/v1.json` in Development, and
`FourDotnet.BoogaBooster.Api.http` has ready-made requests.

---

## Working with AI in this repo

This is the part worth studying. Claude Code and GitHub Copilot CLI **share the same
tooling**: `.claude/` is the single source of truth, and Copilot CLI reads it natively.

| Asset | Lives in | Claude Code | Copilot CLI | Copilot in the IDE |
| --- | --- | :-: | :-: | :-: |
| MCP servers | `.mcp.json` | project scope | workspace scope | — |
| Agents | `.claude/agents/*.md` | ✅ | ✅ | — |
| Skills | `.claude/skills/<name>/SKILL.md` | ✅ | ✅ (also `.github/skills/`) | — |
| Instructions | `CLAUDE.md` (canonical) | ✅ | ✅ | — |
| Instructions | `.github/copilot-instructions.md` (mirror) | — | — | ✅ |
| Slash commands | `.claude/commands/` | ✅ | — | — |
| Slash commands | `.github/prompts/*.prompt.md` | — | ✅ | — |
| CI code review | `.github/code-review/` + `.github/workflows/code-quality-check.yml` | — | ✅ headless | — |

The last row is the one that runs without a human: every pull request into `main` is
reviewed by Copilot CLI in `-p` mode against the rules in the rows above it. See
[Automated code review (CI)](#automated-code-review-ci).

### MCP servers (`.mcp.json`)

| Server | Why it matters |
| --- | --- |
| **`4dotnet-csharp-style-guide`** | **Authoritative** for all C# work: target framework, minimal-API style, DDD / value objects, module and folder structure, testing tooling, the ADRs. Query it *before* writing C#. |
| **`primeng`** | Component API, props, events, theming and accessibility for PrimeNG 22. |
| **`microsoft-learn`** | Official Microsoft / Azure docs and code samples. |

### Agents (`.claude/agents/`)

- **`csharp-expert`** — any `.cs` / `.csproj` / `.slnx` work. Consults the style-guide
  MCP server and verifies ADR compliance before writing a line.
- **`angular-architect`** — anything under the Angular app. Zoneless, signal-first,
  PrimeNG-aware.

### Skills (`.claude/skills/`)

- **`dto-organization`** — DTOs live in the owning module's `Abstractions` project
  under `DataTransferObjects/<Feature>/` as `<Feature>Request` / `<Feature>Response`.
  The skill detects and corrects violations eagerly.
- **`test-coverage`** — module and shared libraries must hold **at least 80 % line
  coverage**, and the skill aims well past the floor.
- **`openspec-*`** — the spec-driven change workflow, below.

### Slash commands

`/opsx:explore`, `/opsx:propose`, `/opsx:apply`, `/opsx:archive` — defined twice, once
in `.claude/commands/opsx/` for Claude and once in `.github/prompts/` for Copilot.
Keep the pair in sync.

### Spec-driven development with OpenSpec

Features here are not written straight into code. They go through
[`openspec/`](openspec/):

1. **`/opsx:explore`** — think the idea through, poke at the problem, clarify.
2. **`/opsx:propose`** — generate a change folder with `proposal.md` (why / what /
   impact), `design.md`, per-capability `specs/*/spec.md`, and `tasks.md`.
3. **`/opsx:apply`** — work the task list, module by module, tests included.
4. **`/opsx:archive`** — publish the capability specs into `openspec/specs/` and move
   the change into `openspec/changes/archive/`.

`openspec/specs/` currently holds **20 published capabilities** (from
`ride-state-machine` to `weather-driven-queue-fill`), and `openspec/changes/archive/`
records the 12 changes that produced them. Two changes are **in flight and
unimplemented** — good first pickups:

- **`passenger-safety`** → over-speed protection: per-component rpm warn/unsafe limits,
  a `safe` / `warning` / `failure` stress reading, and an automatic safety-mode trip.
- **`single-riders-queue`** → a single-riders line that tops up the seats a
  group-consistent load leaves empty.

### The distributable plugin

`plugins/4dotnet-boogabooster/` bundles the agents, skills and MCP server for use in
*other* projects — dual-format, so `claude plugin` and `copilot plugin` both read it.

```
/plugin marketplace add 4dotnet/booga-booster
/plugin install 4dotnet-boogabooster@booga-booster
```

### Rules when you add tooling

- **MCP server** → root `.mcp.json`. Never machine-local (`claude mcp add`), or
  teammates don't get it.
- **Agent or skill** → `.claude/`. Both CLIs pick it up. If it belongs in the plugin,
  sync it into `plugins/4dotnet-boogabooster/` and bump the version.
- **Slash command** → written twice: `.claude/commands/<ns>/<name>.md` **and**
  `.github/prompts/<ns>-<name>.prompt.md`.
- **Repo-wide instructions** → also written twice: `CLAUDE.md` (canonical; read by
  Claude Code *and* Copilot CLI) **and** `.github/copilot-instructions.md` (condensed
  mirror, read by Copilot in the IDE, which does not see `CLAUDE.md`).
- **Agent `tools:` lists** → list **both** vocabularies (`Read` / `Bash` /
  `mcp__server__tool` *and* `read` / `shell` / `server/*`). Unknown names are ignored;
  omitting one leaves the agent tool-less in that CLI.
- **First run of Copilot CLI here** → start it interactively once and trust the folder,
  otherwise `.mcp.json` is never loaded.

---

## Feature ideas to build

A menu of features to build **with** the AI tooling. Each one is scoped to be
finishable in a sitting or two, names the modules it touches, and is deliberately
under-specified — pinning down the requirements with `/opsx:explore` and
`/opsx:propose` is half the exercise.

Suggested loop for every one of them:

```
/opsx:explore  -> sharpen the idea
/opsx:propose  -> proposal + design + specs + tasks
/opsx:apply    -> csharp-expert / angular-architect do the work,
                  test-coverage keeps the floor
/opsx:archive  -> publish the capability
```

### Warm-up — one module, no new concepts

1. **Ride statistics panel.** Count completed cycles, passengers carried, and total run
   time in the DigitalTwin module; expose them on telemetry and show them in a new
   dashboard panel. *(DigitalTwin + frontend)*
2. **Brake-all button.** One operator action that engages or releases all 16 gondola
   brakes, with the state machine guarding when it is allowed.
   *(DigitalTwin + frontend)*
3. **Weather forecast.** Project the weather simulation a few minutes ahead and render
   a small forecast strip in the weather panel. *(Weather + frontend)*
4. **Runtime ride parameters.** Turn a slice of `RideParameters` into bound options
   with an operator-only "engineering" panel. Mind determinism.
   *(DigitalTwin + frontend)*

### Core — a real feature across the stack

5. **Ride programmes.** A named sequence of timed set-points ("Gentle", "Classic",
   "Booster") that the ride executes automatically: ramp the mill to X, spin the hubs
   up, hold, reverse, coast. This is what the empty **`Controller` module** is for — a
   scripted programme runner driving the twin through its command handlers.
6. **Ticketing and fast pass.** A second queue with priority ordering, a per-guest
   ticket type, and a boarding policy that interleaves the two lines fairly.
   *(Queue + DigitalTwin + frontend)*
7. **Maintenance and wear.** Accumulate wear per driven axis from run time and load;
   above a threshold the ride demands a maintenance stop and refuses to start until
   serviced. *(DigitalTwin + a new module + frontend)*
8. **Operator audit log.** Every command, transition and interlock trip recorded as an
   integration event, with a filterable timeline in the UI. Good practice at
   cross-module events. *(IntegrationMessages + new module + frontend)*
9. **Guest satisfaction.** Score each rider on wait time, weather, ride intensity and
   G-force; feed the aggregate back into how willing guests are to queue.
   *(Queue + DigitalTwin + Weather)*
10. **Wind loading on the physics.** Let the Weather module's wind actually perturb the
    gondola pendulums and add drag torque to the mill — and make the wind cutoff in
    `docs/appendix-parameters.md` a real interlock.
    *(Weather → DigitalTwin; start in `docs/`)*

### Ambitious — architecture-shaped

11. **Persistence.** Everything is in-memory today (`InMemoryRideQueueStore`,
    `RideStore`, `WeatherStore`). Add a real store behind those interfaces, wired as an
    Aspire resource, so a restart does not forget the park.
12. **Multiple rides.** The ride id is a single well-known GUID in two places today
    (`QueueModuleOptions.RideIds`, `DigitalTwinModuleOptions.RideId`). Make the park
    multi-ride: a ride registry, per-ride queues and twins, and a park overview screen.
13. **Emergency scenarios.** Injectable faults — a seized bearing, a stuck restraint, a
    power brown-out — plus the interlocks and operator procedures each one demands. A
    great fit for property-based tests.
14. **Replay and time travel.** The physics is deterministic: record the command stream
    and replay a session at variable speed, scrubbable from the dashboard.
15. **Park-wide guest flow.** Guests wander between attractions, choose queues by
    length and weather, and leave when they have had enough. Turns the queue simulation
    into an agent-based one.

### Exercises about the tooling itself

16. **Write a new skill.** For example, one enforcing that every new module ships an
    `AddXxxModule()` + `MapXxxEndpoints()` pair and a matching test project. Put it in
    `.claude/`, then sync it into the plugin.
17. **Write a new agent.** A `physics-reviewer` that checks any change to
    `DigitalTwin/Domain` against the derivations in `docs/` and flags magic numbers
    that belong in `RideParameters`.
18. **Add an MCP server.** Wire up something the project could genuinely use and add it
    to the root `.mcp.json` so the whole team gets it.
19. **Close the docs/code terminology gap.** `docs/` says *cart*, the code says
    *gondola*. Pick one and make the repo consistent — a small, well-defined,
    surprisingly wide-reaching refactor.
20. **Adopt PrimeNG for real.** The library is installed and mandated but unused.
    Convert one panel to PrimeNG components, driving the whole change through the
    `angular-architect` agent and the `primeng` MCP server — then the next feature has
    a pattern to copy. Keep the AXE specs green.

---

## Conventions, briefly

- **C#:** consult the `4dotnet-csharp-style-guide` MCP server **first**, every time. It
  outranks anything you find in this repo; if they disagree, follow the guide and flag
  the discrepancy.
- **DTOs:** owning module's `Abstractions` project, `DataTransferObjects/<Feature>/`.
- **Tests:** xUnit v3 + Moq + Bogus, no FluentAssertions. At least 80 % line coverage
  on module and shared libraries.
- **Physics:** no magic numbers — every constant belongs in `RideParameters`, with the
  derivation in `docs/`.
- **Angular:** see the app's own `CLAUDE.md`; accessibility is not optional.

## License

[MIT](LICENSE)
