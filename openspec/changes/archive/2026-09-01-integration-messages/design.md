## Context

BoogaBooster is a .NET 10 modular monolith (ADR-0004) wired together by .NET Aspire. Bounded-context modules (Weather, Controller, DigitalTwin, Queue) currently have no event-based communication path. We want modules to raise integration events and react to them without direct module-to-module code references, over a real message broker, so the same contracts work if a module is later split into its own service.

Constraints and inputs:
- The style guide (via the `4dotnet-csharp-style-guide` MCP server) is authoritative: .NET 10 (ADR-0001), minimal APIs only (ADR-0002), modular monolith with `Shared/` for cross-cutting code (ADR-0004), endpoints live in the module (ADR-0007).
- Transport is **Dapr PubSub** with **RabbitMQ** as the broker.
- The Dapr pub/sub component MUST be configured with the **Aspire Community Toolkit for Dapr** in `AppHost.cs`, **not** via static Dapr component YAML. Reference pattern: [ThePrey AppHost.cs](https://raw.githubusercontent.com/hexmasternl/the-prey/refs/heads/main/src/Aspire/ThePrey.Aspire.AppHost/AppHost.cs).
- All contracts centralize in `FourDotnet.BoogaBooster.IntegrationMessages` under `Shared/`.

## Goals / Non-Goals

**Goals:**
- One central library that owns every integration event contract with a strict naming/namespace convention.
- A single `AddBoogaBoosterIntegrationMessages()` call that gives any host a publisher.
- Topic-per-event declared declaratively via `[TopicName]`, used symmetrically by publisher and subscriber.
- Programmatic Aspire+Dapr wiring of RabbitMQ and the pub/sub component (no YAML), with Dapr sidecars on publishing/subscribing projects.
- Minimal-API subscription endpoints using Dapr `WithTopic`.

**Non-Goals:**
- No cloud broker provisioning (e.g. Azure Service Bus) — RabbitMQ + local Aspire only for now.
- No message versioning/schema-registry, outbox/idempotency, dead-letter, or retry policy design (noted as future work).
- No concrete business events beyond a reference `WeatherChangedIntegrationEvent` used to validate the pattern.
- No Dapr state store, actors, or bindings — pub/sub only.

## Decisions

### Decision: Central contracts library in `Shared/`, abstractions-free
`FourDotnet.BoogaBooster.IntegrationMessages` (project at `src/Shared/FourDotnet.BoogaBooster.IntegrationMessages`) holds the event records, the `[TopicName]` attribute, the publisher abstraction + implementation, and `AddBoogaBoosterIntegrationMessages()`.

- **Why**: Contracts are shared kernel; centralizing avoids module-to-module references (ADR-0004) and gives one place to discover all events. Because integration events are DTO-style contracts (not domain entities), they do not need a separate `.Abstractions` project the way modules do.
- **Alternatives considered**: (a) Per-module event contracts in each module's `.Abstractions` project — rejected: consumers would reference producer modules, reintroducing coupling. (b) A separate `.Abstractions` split for the publisher interface — rejected as unnecessary ceremony for a shared kernel already meant to be referenced widely.

### Decision: Events are immutable `record` types under `Events.<Module>`, suffixed `IntegrationEvent`
Each event is a `sealed record` in `FourDotnet.BoogaBooster.IntegrationMessages.Events.<Module>` named `<Something>IntegrationEvent`.

- **Why**: Records give value semantics and immutability appropriate for messages; the namespace-by-origin-module + suffix convention makes ownership and intent obvious and greppable.
- **Alternatives**: A shared marker base class/`IIntegrationEvent` interface. We MAY add a lightweight marker interface to constrain the publisher's generic signature, but avoid an inheritance hierarchy — messages stay flat DTOs.

### Decision: Topic resolved from `[TopicName]` via reflection, cached
A `TopicNameAttribute(string name)` decorates each event. The publisher reads it (reflection, cached per type) to determine the Dapr topic; publishing an event lacking the attribute throws a descriptive exception.

- **Why**: Keeps the topic co-located with the contract, single source of truth shared by publisher and (by convention) the subscriber's `WithTopic`. Caching avoids per-publish reflection cost.
- **Alternatives**: Passing the topic string at publish time (rejected — error-prone, duplicated) or a naming convention derived from the type name (rejected — too implicit, no room for hyphenated broker-friendly names).

### Decision: Publisher wraps Dapr `DaprClient.PublishEventAsync`
`AddBoogaBoosterIntegrationMessages()` registers Dapr (`AddDaprClient`) and an `IIntegrationEventPublisher` whose `PublishAsync(event, ct)` resolves the topic and calls `PublishEventAsync(pubsubName, topic, event, ct)`. The pub/sub component name is a shared constant (e.g. `"pubsub"`) matching the AppHost registration.

- **Why**: Thin wrapper keeps Dapr as the transport detail behind a small app-facing abstraction; the constant keeps the component name consistent between AppHost and app.
- **Alternatives**: Publishing raw via `DaprClient` in each module (rejected — leaks transport, no topic-resolution guarantee).

### Decision: Subscriptions are minimal-API endpoints with `WithTopic`, mapped in the module (ADR-0007)
Consumers map a `POST` minimal-API endpoint (JSON body = the event) annotated with `.WithTopic(pubsubName, "topic")` from `Dapr.AspNetCore`. `app.MapSubscribeHandler()` / Dapr endpoint routing is enabled on the API host. Endpoint mappings live in the owning module and are exposed via the module's `Map...Endpoints()` extension (ADR-0007), not in the API project.

- **Why**: ADR-0002 mandates minimal APIs; ADR-0007 mandates endpoints in modules. `WithTopic` keeps subscription declarative and code-first (no subscription YAML).
- **Alternatives**: Static Dapr subscription YAML (rejected by requirement) or the streaming subscription API (rejected — heavier, not needed for HTTP endpoints).

### Decision: AppHost wires RabbitMQ + Dapr pub/sub with the Community Toolkit, no YAML
In `AppHost.cs`: add `CommunityToolkit.Aspire.Hosting.Dapr` and `Aspire.Hosting.RabbitMQ`. Provision RabbitMQ with secret parameters and the management plugin; build the AMQP connection string via `ReferenceExpression`; register the pub/sub component with `AddDaprComponent("pubsub", "pubsub.rabbitmq")` (or the toolkit `AddDaprPubSub` equivalent) and `.WithMetadata("connectionString"/"username"/"password", ...)`, `.WaitFor(rabbitmq)`; attach the sidecar to the API via `.WithDaprSidecar(o => o.WithReference(pubSub))` and `.WithReference(rabbitmq).WaitFor(rabbitmq)`. This mirrors the ThePrey reference.

- **Why**: The toolkit generates the component/secret-store plumbing from code, satisfying the "no YAML" requirement while staying idiomatic Aspire.
- **Alternatives**: Hand-authored `components/pubsub.yaml` + `--resources-path` (explicitly rejected by requirement).

## Risks / Trade-offs

- **Dapr runtime dependency for local dev** → The Aspire Dapr sidecars require the Dapr CLI/runtime installed on developer machines. Mitigation: document the prerequisite in the change tasks / README and fail with a clear message if the sidecar can't start.
- **Publisher/subscriber topic drift** (`[TopicName]` vs `WithTopic` string) → silent message loss. Mitigation: expose the topic as a shared `const`/static accessor derived from the attribute so subscribers reference the same value instead of retyping the literal; cover with a test.
- **Reflection cost / trimming** → attribute reflection could be a hot path or break under trimming/AOT. Mitigation: cache topic-per-type in a `ConcurrentDictionary`; keep event types non-trimmable (they're referenced) — revisit if AOT is adopted.
- **No delivery guarantees / idempotency yet** → at-least-once delivery from Dapr means consumers may see duplicates. Mitigation: out of scope now; document that handlers should be idempotent and revisit outbox/dedup later.
- **RabbitMQ-only, no cloud broker** → prod topology will differ. Mitigation: because the component is code-configured behind a component name, swapping the broker later is an AppHost-only change; app code and contracts are unaffected.
- **Style-guide compliance** → all C# (project layout, minimal APIs, records, endpoint extension methods) must be validated against the `4dotnet-csharp-style-guide` MCP server during implementation, per CLAUDE.md.

## Migration Plan

New capability only — additive, no rollback of existing behavior needed. Rollout: (1) add the library + AppHost wiring behind existing services; (2) validate publish/subscribe with the reference Weather event end-to-end via the Aspire dashboard + RabbitMQ management UI; (3) adopt in real module flows incrementally. Rollback = remove the sidecar/component registration and the library reference; no data migration involved.

## Open Questions

- Component name constant: standardize on `"pubsub"` — confirm no other pub/sub component will need a different name.
- Do we want a shared marker interface (`IIntegrationEvent`) to constrain the publisher generic, or keep it fully open? (Leaning: minimal marker interface.)
- Should `AddBoogaBoosterIntegrationMessages()` also register subscription infrastructure (e.g. `MapSubscribeHandler`) helpers, or keep publish and subscribe registration separate?
