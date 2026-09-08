## Context

ADR-0009 splits handler instrumentation between the CQRS base classes and the concrete
handler: the base *"start[s] the activity, tim[es] the operation, record[s] success/failure
counters"* and *"each concrete handler only adds the attributes and domain metrics
specific to it."* In this repo the base half is done and done well —
`Shared/Core/Cqrs/CommandHandler.cs` and `QueryHandler.cs` wrap `ExecuteAsync` in a
`HandlerTelemetryScope` that tags operation and kind, records `AddException` plus error
status on failure, and emits `boogabooster.handler.invocations` and
`boogabooster.handler.duration` against the one shared `Meter`. `ServiceDefaults` registers
that single source and meter, satisfying ADR-0009's one-place rule.

What is missing is the concrete half. `EnrichActivity` is a `protected virtual` no-op, and
only the Queue module's two handlers override it. The other 14 handlers inherit an empty
hook. Separately, the three `BackgroundService` implementations and
`DaprIntegrationEventPublisher` predate the telemetry plumbing entirely and carry
`ILogger` only, which ADR-0009 explicitly rejects: *"Sprinkling ad-hoc logging is not
enough."*

Two constraints shape the approach. First, the physics loop runs at 120 Hz — 7,200 ticks a
minute — so the naive "span per unit of work" pattern that fits a handler does not fit a
tick. Second, `RideLoadingCoordinator.RunLoadingPassAsync` returns `Task`, so a caller
cannot currently see whether a pass boarded anyone.

The Queue module's existing work is the template to follow, not a thing to redo:
`QueueHandlerTelemetryTests.cs` establishes the `ActivityListener` test pattern and
`GetQueueStatusQueryHandler` establishes the tagging style, including the deliberate
choice to record counts rather than the queued people themselves.

## Goals / Non-Goals

**Goals:**

- Bring all 16 handlers up to the ADR-0009 contract by overriding the hooks the base
  classes already expose, with no change to the base classes themselves.
- Instrument the three background services and the integration publisher, at a cadence
  proportionate to how often each acts.
- Publish the domain metrics that make the twin legible through the existing shared
  `Meter`.
- Keep metric cardinality bounded, and keep passenger identity off every signal.
- Leave a written convention so the next handler is instrumented as it is written.

**Non-Goals:**

- **No change to `ServiceDefaults` or to OTEL configuration.** The single
  `ActivitySource`/`Meter` it registers already collects everything added here. Touching it
  would be the ADR-0009 violation this change exists to fix.
- **No change to the CQRS base classes.** Their hooks are the extension point; the gap is
  that nothing used them.
- **No new observability signals beyond traces and metrics.** Structured logs already flow
  through the OTEL logging provider with trace correlation; ADR-0009 forbids adding a
  competing stack.
- **No physics, domain or HTTP-contract change.** In particular this change does *not*
  address the separate `docs/`-vs-code gaps found in the same audit (the missing over-G
  interlock, the NaN watchdog, the absent G/jerk/energy telemetry channels). Those are
  behavioural changes to the twin and belong in their own proposal — instrumenting a
  simulation is not the same as adding safety logic to it.
- **No new package dependencies.**

## Decisions

### D1. Tag through the existing `EnrichActivity` hooks, one override per handler

Each handler gets a `protected override void EnrichActivity(Activity activity, TCommand
command)` recording its own inputs. This is the mechanism ADR-0009 names, costs nothing
when no listener is attached (the base only calls it when `scope.Activity` is non-null),
and keeps the tagging next to the code whose inputs it describes.

*Alternative considered — reflect over the command record and tag every property
automatically.* Rejected: it is precisely the thing ADR-0009's "identifiers and inputs
that **matter** for diagnosis" wording argues against. It would put a passenger's weight
on a span by default, defeat the no-personal-data rule silently, and produce noise instead
of signal. Explicit is worth the 14 small overrides.

### D2. Payload-free commands tag the state they found, not nothing

`StartRideCommand`, `StopRideCommand` and `StartStrongWindCommand` carry no fields. Rather
than leave the hook unimplemented, each records the state its work depended on — the
ride's lifecycle state as the command found it, the weather regime in effect. This is what
makes one `StartRide` span distinguishable from another during an incident, and it is why
the spec requires it rather than exempting empty commands.

### D3. Attribute naming: `<module>.<subject>.<detail>`

Module-prefixed, dot-delimited, lower-case, snake_case within a segment — extending the
convention the Queue handlers already set (`queue.ride.id`,
`queue.weather.nice_weather.observed`). The prefixes are `ride.`, `queue.`, `weather.`,
`messaging.`. Names are declared as `const string` in one place per module rather than
repeated as literals at each call site, so a rename is a single edit and tests can assert
against the same constant the production code uses.

*Alternative considered — OpenTelemetry semantic conventions.* They cover HTTP, messaging
and database attributes, and the messaging ones are worth following for the publisher
(`messaging.destination.name`, `messaging.system`). But there is no semantic convention for
a carnival-ride digital twin, so domain attributes get our own prefix rather than an
invented `otel.*`-adjacent name.

### D4. The 120 Hz loop is measured, not spanned

`RideSimulationService` gets a counter (`boogabooster.ride.simulation.ticks`) and a
histogram of tick duration, and **no activity per tick**. At 120 spans a second a trace
backend would be swamped, the Aspire dashboard unusable, and the interesting signal — a
tick that ran long — buried. A histogram answers "how is the loop performing" far better
than 7,200 spans a minute, and it is what the ADR's own "histogram for its duration"
wording asks for.

The loading pass *is* worth a span, because it acts intermittently: a pass that boards a
group is a discrete, causally interesting event. So the activity is started inside
`RideLoadingCoordinator`, and only for a pass that actually boards someone — the common
case (nothing to board) stays free.

*Alternative considered — a span per tick with head sampling at, say, 1 %.* Rejected:
sampling a deterministic fixed-step loop yields a random 1 % of near-identical spans,
which tells you less than the histogram does, and it would make the *interesting* long
tick 99 % likely to be dropped.

### D5. `RideLoadingCoordinator` instruments itself; its signature does not change

`RunLoadingPassAsync` stays `Task`-returning. The coordinator starts its own activity and
records its own counter internally, so `RideSimulationService` needs no new return value to
inspect and no knowledge of what a pass did. Instrumentation stays with the code that has
the facts.

*Alternative considered — return a `LoadingPassResult` record and let the service
instrument.* Rejected: it widens a public API purely for telemetry's benefit, and pushes
tagging away from the code that knows why a group was skipped.

### D6. Metric tags stay bounded; identifiers live on spans only

Ride ids, group ids, gondola indices and passenger weights are recorded as **span
attributes** and never as **metric tags**. A span is already one record per operation, so a
high-cardinality attribute costs nothing there; a metric tag multiplies the time series.
Gondola index alone would 16× every series it touched, and ride id is unbounded. Metric
tags are drawn from fixed sets only: operation name, lifecycle state, topic, precipitation
type, outcome.

### D7. The publisher span is a child of the handler's, and follows messaging conventions

`DaprIntegrationEventPublisher` starts its activity from the same shared `ActivitySource`,
which makes it a child of the ambient handler activity automatically via `Activity.Current`
— so publishing from `RideQueueService` appears nested under the work that caused it,
giving the end-to-end trace ADR-0009 is after. It is tagged with
`messaging.destination.name` (the resolved topic), `messaging.system` (`dapr`) and the
event type name, with `ActivityKind.Producer`.

Its counter is tagged by topic and outcome — both bounded sets — so a topic that starts
failing is visible without a trace search.

### D8. One telemetry test class per module, reusing the established pattern

Each module test project gains a `<Module>HandlerTelemetryTests` built the way
`QueueHandlerTelemetryTests` already is: an `ActivityListener` filtered to
`BoogaBoosterTelemetry.SourceName`, sampling `AllDataAndRecorded`, collecting stopped
activities. Metrics are asserted with the built-in `MeterListener` filtered on
`instrument.Meter.Name`, exactly as `Tests/FourDotnet.BoogaBooster.Core.Tests/HandlerInstrumentationTests.cs`
already does — **not** `MetricCollector<T>`, which would pull in
`Microsoft.Extensions.Diagnostics.Testing` for no gain over a pattern the repo has already
settled. Tests assert against the same attribute-name constants the production code uses,
so a rename cannot leave a test asserting a dead string.

This is also what makes the change durable: the spec's last requirement ("a handler that
stops tagging fails the build") turns a convention into a gate, which is the difference
between fixing this gap and fixing it again next quarter.

### D9. Where the shared instruments live

New domain instruments are declared in `Shared/Core/Observability/BoogaBoosterTelemetry.cs`
alongside the two handler instruments already there, because that file is already the one
place both the meter and the source are defined and `ServiceDefaults` already references
its `SourceName`. Module-specific attribute-name constants live in the owning module, not
in `Core` — `Core` must not accumulate module vocabulary.

*Alternative considered — a `Meter` per module.* Rejected outright: ADR-0009 mandates *"a
single well-known `ActivitySource` name and `Meter` name per service"*, and
`ServiceDefaults` registers exactly one of each.

## Risks / Trade-offs

**[Metric cardinality explosion] → D6 confines every metric tag to a fixed, enumerable
set, and the spec makes it a testable requirement rather than a review habit.** The
specific traps: gondola index (16×), ride id (unbounded), passenger weight (continuous).
All three are span-only.

**[Instrumenting the 120 Hz loop degrades the physics] → D4 keeps activities out of the
tick path entirely.** What remains per tick is a counter `Add` and a `Stopwatch` timestamp
pair — nanoseconds against an 8.33 ms budget. Determinism is unaffected because nothing in
the instrumentation feeds back into state: no clock read enters the tick, which is the
`docs/01 §1.1` rule.

**[Personal data reaching a span] → passenger name and individual queued people are
excluded by construction, and asserted absent by test.** The boarding span records hub,
gondola, seat and whether a weight was supplied — not the weight itself, and not the name.
The existing Queue handler already sets this precedent by tagging counts instead of people.

**[14 near-identical overrides invite copy-paste drift] → shared attribute-name constants
per module, so the repeated part is a reference rather than a literal.** The residual
duplication is accepted: it is the cost of D1's explicitness, and it is small and local.

**[The change is broad and touches 18 files] → it is additive and mechanically verifiable.**
No existing behaviour, signature or contract changes except the internals of the four
uninstrumented services, so the risk of regression is low and the full test suite plus the
coverage floor is a sufficient gate. The work also splits cleanly along module lines, so it
can land as several reviewable commits rather than one.

**[Domain metrics are guesswork until someone reads a dashboard] → the set is chosen to
match measures the specs already treat as meaningful** (boarded passenger count, groups
queued, transitions accepted/rejected, weather regime), so they are not invented from
nothing. Expect one round of adjustment once they are visible in the Aspire dashboard;
adding an instrument later is cheap, and removing an unused one is cheaper.

## Migration Plan

No migration. The change is additive instrumentation: no schema, no stored state, no wire
contract. Deploying it changes what the Aspire dashboard shows and nothing else, and
reverting it is a plain revert with no cleanup.

Suggested landing order, each step independently green:

1. `Core` — new shared instruments and any attribute-name plumbing.
2. `IntegrationMessages` — the publisher (smallest, and exercises the child-span decision
   D7 before anything depends on it).
3. `Weather` — 3 handlers plus `WeatherSimulationService`.
4. `Queue` — `RideQueueFillerService` and the queued-groups metric (its handlers are
   already done).
5. `DigitalTwin` — 11 handlers, `RideSimulationService`, `RideLoadingCoordinator`. Largest,
   and benefits from the conventions being settled by then.
6. The written convention (skill or `CLAUDE.md` note), last, describing what was actually
   built.

## Open Questions

- **Should the ride-state-transition counter also tag the target state?** Target state is a
  bounded set (7 values) so it is cardinality-safe, and it would let a dashboard show which
  transitions get rejected most. Leaning yes; deferred to implementation because it is a
  one-tag decision with no design consequence.
- **Does the loading-pass span want a tag for *why* a pass boarded nobody** (ride full vs.
  queue empty vs. no waiting group fits)? D4 keeps the no-op pass span-free, so this would
  mean either spanning every pass or folding the reason into a counter tag. The three
  reasons are a bounded set, so a counter tag is the cheap option — but it is only worth
  adding if operators actually ask why boarding stalled. Deferred until the dashboard
  exists.

*Resolved during design:* metrics are asserted with the built-in `MeterListener`, so D8
adds no package reference and the proposal's "no new dependencies" claim holds.
