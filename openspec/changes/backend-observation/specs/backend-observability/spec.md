## ADDED Requirements

### Requirement: Every command handler tags its span with its own inputs

Every command handler in every module SHALL override `EnrichActivity` to record the
identifiers and inputs that matter for diagnosis, on top of the operation name and kind
the CQRS base class already tags. A handler whose command carries no payload SHALL still
record the state its work depends on or produced, so that no handler's span is
indistinguishable from any other invocation of it.

Attribute values SHALL be scalars (string, number, boolean) so the exporter records them
without flattening, and SHALL NOT include secrets or personal data — no passenger name,
and no individual queued person.

#### Scenario: An engine-power command records the engine and the setting

- **WHEN** a `SetMainEnginePowerCommand` for 60 % is handled
- **THEN** the completed activity carries the commanded percentage
- **AND** it carries which drive was commanded, so the main-engine and hub-engine spans are distinguishable

#### Scenario: A gondola-addressed command records which gondola

- **WHEN** a `SetGondolaBrakeCommand` or a `BoardPassengerCommand` for hub 2, gondola 3 is handled
- **THEN** the completed activity carries the hub index and the gondola index
- **AND** for a boarding command it also carries the seat addressed and whether a weight was supplied

#### Scenario: A payload-free command records the state it acted on

- **WHEN** a `StartRideCommand` or `StopRideCommand` is handled
- **THEN** the completed activity carries the ride's lifecycle state as the command found it

#### Scenario: No personal data reaches a span

- **WHEN** a passenger with a known name and weight is boarded
- **THEN** no attribute on the completed activity contains the passenger's name

### Requirement: Every query handler tags both the request and what it read

Every query handler SHALL override `EnrichActivity` to record what was asked for, and
`EnrichActivityWithResponse` to record the shape of what came back — counts, sizes and
scalar readings, never the payload itself. A query whose request carries no payload SHALL
still describe its response.

#### Scenario: A telemetry query records the shape of the snapshot it read

- **WHEN** a `GetRideTelemetryQuery` is handled
- **THEN** the completed activity carries the ride's lifecycle state, the boarded passenger count, and the mill's rotation speed from the snapshot returned

#### Scenario: A weather query records the conditions it read

- **WHEN** a `GetWeatherQuery` is handled
- **THEN** the completed activity carries the weather regime and whether the conditions count as nice weather

#### Scenario: A query response is summarized, not copied

- **WHEN** a query returns a collection
- **THEN** the activity carries the collection's count
- **AND** does not carry the collection's elements

### Requirement: A failing handler is visible as a failure

A handler that throws SHALL leave an activity whose status is error and which carries the
exception, and SHALL record its invocation against an `error` outcome — including when the
failure is a `DomainValidationException` that an endpoint later translates to a 400. The
attributes the handler contributed before the failure SHALL remain on the span.

#### Scenario: A rejected transition is traceable

- **WHEN** a `RequestRideStateTransitionCommand` is rejected because its guard fails
- **THEN** the completed activity has error status and carries the exception
- **AND** it still carries the requested target state and the state the ride was in

#### Scenario: A failed invocation is counted as failed

- **WHEN** a handler throws
- **THEN** the handler-invocation counter records the invocation with outcome `error`

### Requirement: Background services report their work as traces and metrics

Every background service SHALL emit traces and metrics through the shared
`ActivitySource` and `Meter`. A service that acts intermittently SHALL start an activity
per pass. A service that acts at the physics tick rate SHALL NOT start an activity per
tick — the volume would cost more than it reveals — and SHALL instead report its work
through metrics plus an activity only when a pass does something noteworthy.

#### Scenario: The ride simulation reports its tick rate and cost

- **WHEN** the ride simulation loop has run for a period
- **THEN** a counter reports the number of ticks executed
- **AND** a histogram reports how long a tick took
- **AND** no activity was started for an individual tick

#### Scenario: A loading pass that boards a group is traced

- **WHEN** a simulation tick's loading pass boards a group
- **THEN** an activity is started for that pass, carrying the ride addressed and the number of passengers boarded

#### Scenario: The queue filler traces each fill pass

- **WHEN** the queue filler service completes a fill pass
- **THEN** the completed activity carries the ride filled, the number of groups added and the number of people added
- **AND** a counter records the groups queued

#### Scenario: The weather simulation traces each advance

- **WHEN** the weather simulation advances the weather
- **THEN** the completed activity carries the resulting regime and nice-weather reading

#### Scenario: A background failure is recorded, not swallowed silently

- **WHEN** a background pass throws and the service logs and continues
- **THEN** the pass's activity has error status and carries the exception

### Requirement: Integration-event publishing is traced and counted

The integration-event publisher SHALL start an activity for each publish, tag it with the
topic and the event type, record error status when the transport fails, and count
published events tagged by topic and outcome. The topic SHALL come from the event's
`[TopicName]`, so the span reports the same topic the transport used.

#### Scenario: A published event is traceable to its topic

- **WHEN** an integration event is published
- **THEN** the completed activity carries the resolved topic name, the event type name and the pub/sub component name

#### Scenario: A publish failure is visible

- **WHEN** the transport throws while publishing
- **THEN** the activity has error status and carries the exception
- **AND** the publish counter records the attempt with outcome `error`

#### Scenario: The publish span is a child of the work that caused it

- **WHEN** a handler publishes an integration event during its execution
- **THEN** the publish activity is a child of the handler's activity

### Requirement: Domain metrics are published through the shared Meter

The modules SHALL publish the domain measures that make the twin legible through the one
shared `Meter`, never through a meter of their own: simulation ticks and tick duration,
passengers boarded, ride-state transitions (tagged by outcome so rejected requests are
countable), weather disturbances raised, groups and people queued, and integration events
published.

#### Scenario: Boarding is counted

- **WHEN** a passenger is boarded
- **THEN** a counter records the boarding

#### Scenario: A rejected transition is countable separately from an accepted one

- **WHEN** a state transition is requested and rejected
- **THEN** the transition counter records it with a rejected outcome, distinguishable from an accepted transition

#### Scenario: All metrics share one meter

- **WHEN** the meter named for the solution is collected
- **THEN** every metric this change introduces is collected from it
- **AND** no module creates a `Meter` or `ActivitySource` of its own

### Requirement: Attribute and metric names follow one convention

Span attribute names SHALL be dot-delimited, lower-case, and prefixed with the owning
module (`ride.`, `queue.`, `weather.`, `messaging.`). Metric names SHALL be prefixed
`boogabooster.`. Metric tags SHALL be low-cardinality: a tag SHALL NOT carry a value drawn
from an unbounded or large set — no ride id, group id, gondola index or passenger weight as
a metric tag — while such identifiers are permitted as span attributes, where each span is
already a distinct record.

#### Scenario: Identifiers are span attributes, not metric tags

- **WHEN** a handler records the ride id it acted on
- **THEN** the ride id appears as a span attribute
- **AND** no metric is tagged with the ride id

#### Scenario: Metric tags are drawn from a bounded set

- **WHEN** any metric this change introduces is recorded
- **THEN** each of its tags takes a value from a fixed, enumerable set such as an operation name, a lifecycle state, a topic or an outcome

### Requirement: Observability is verified by tests

Each module's test project SHALL cover the attributes its handlers contribute and the
metrics its background work emits, so an instrumentation regression fails the build rather
than being found by a later audit. Tests SHALL capture activities with an
`ActivityListener` scoped to the shared source name and metrics with a `MetricCollector`,
following the pattern already established for the Queue module.

#### Scenario: Every handler has a telemetry test

- **WHEN** the backend test suite runs
- **THEN** each command and query handler has at least one test asserting the attributes it contributes

#### Scenario: A handler that stops tagging fails the build

- **WHEN** a handler's `EnrichActivity` override is removed
- **THEN** at least one test fails
