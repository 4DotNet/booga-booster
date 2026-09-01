## Why

The modular monolith's bounded contexts (Weather, Controller, DigitalTwin, Queue, ...) currently have no sanctioned way to communicate through events. Modules must be able to raise integration events without taking a direct code dependency on each other, and consumers must be able to react to those events in a decoupled, transport-agnostic way. We need a single, central contract library plus a supported publish/subscribe transport so that cross-module (and future cross-service) messaging is consistent from day one.

## What Changes

- Introduce a new **`FourDotnet.BoogaBooster.IntegrationMessages`** shared library that is the single source of truth for every integration event that can be published across the system.
- All integration events live under an `Events` namespace, then a per-originating-module namespace (e.g. `Events.Weather`), and are always suffixed with `IntegrationEvent` (e.g. `Events.Weather.WeatherChangedIntegrationEvent`).
- Introduce a `[TopicName("name-of-topic")]` attribute applied to each integration event to declare the Dapr pub/sub topic it is published to.
- Provide a single `AddBoogaBoosterIntegrationMessages()` extension method that registers a **publisher service** any module can inject to publish integration events.
- Publishing and subscribing use **Dapr PubSub**, with **RabbitMQ** as the broker.
- Wire the Dapr pub/sub component and RabbitMQ in the Aspire **AppHost** using the **Aspire Community Toolkit for Dapr** — configured **programmatically in `AppHost.cs`**, **NOT** via static Dapr component YAML files. Every subscribing/publishing project gets a Dapr sidecar referencing the pub/sub component.
- Establish the **subscriber pattern**: consumers subscribe by declaring a minimal API endpoint annotated with Dapr's `WithTopic("topic-name")`.

## Capabilities

### New Capabilities

- `integration-messaging`: The central integration-messages contract library — event organization/naming conventions, the `[TopicName]` attribute, the publisher service and its `AddBoogaBoosterIntegrationMessages()` registration, and the minimal-API `WithTopic` subscriber pattern.
- `dapr-pubsub-infrastructure`: The Aspire AppHost wiring that provisions RabbitMQ and the Dapr pub/sub component via the Aspire Community Toolkit for Dapr (no YAML), and attaches Dapr sidecars to the API project(s).

### Modified Capabilities

<!-- None — no existing specs change their requirements. -->

## Impact

- **New project**: `src/Shared/FourDotnet.BoogaBooster.IntegrationMessages` added to `BoogaBooster.slnx`.
- **AppHost**: `src/Aspire/FourDotnet.BoogaBooster.Aspire.AppHost/AppHost.cs` gains RabbitMQ, the Dapr pub/sub component, and a Dapr sidecar on the API project.
- **NuGet dependencies**: `CommunityToolkit.Aspire.Hosting.Dapr` and `Aspire.Hosting.RabbitMQ` in the AppHost; `Dapr.AspNetCore` (Dapr client + subscribe support) in the API and any subscribing module.
- **API project**: `FourDotnet.BoogaBooster.Api` references the IntegrationMessages library, calls `AddBoogaBoosterIntegrationMessages()`, and maps Dapr subscription endpoints.
- **Modules**: any module may reference the IntegrationMessages library to publish or subscribe; no module-to-module code coupling is introduced.
- **Local dev**: RabbitMQ runs as an Aspire-managed container (management plugin exposed); requires the Dapr CLI/runtime available to the Aspire Dapr sidecars.
