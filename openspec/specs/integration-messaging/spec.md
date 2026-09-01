# integration-messaging Specification

## Purpose

Defines the central integration-messages library: event naming, topic declaration, publisher registration, and the minimal-API subscriber pattern.

## Requirements

### Requirement: Central integration-messages library

The system SHALL provide a single shared library, `FourDotnet.BoogaBooster.IntegrationMessages`, located in the solution's `Shared` folder, that is the one authoritative home for every integration event that any module can publish. Modules SHALL depend on this library (never on each other's module projects) to publish or subscribe to integration events.

#### Scenario: Library is referenced instead of module-to-module coupling

- **WHEN** a module needs to publish or consume an integration event
- **THEN** it references `FourDotnet.BoogaBooster.IntegrationMessages` for the event contract
- **AND** it does not add a project reference to another module's project

#### Scenario: Library lives under Shared

- **WHEN** the solution structure is inspected
- **THEN** `FourDotnet.BoogaBooster.IntegrationMessages` is located under `src/Shared` and included in `BoogaBooster.slnx`

### Requirement: Integration event namespace and naming conventions

Every integration event type SHALL be declared under the `FourDotnet.BoogaBooster.IntegrationMessages.Events` namespace, followed by a namespace named after the module the event originates from (e.g. `Events.Weather`). Every integration event type name SHALL be suffixed with `IntegrationEvent`.

#### Scenario: Weather event follows the convention

- **WHEN** the Weather module defines an event announcing a weather change
- **THEN** the type is `FourDotnet.BoogaBooster.IntegrationMessages.Events.Weather.WeatherChangedIntegrationEvent`

#### Scenario: Non-conforming type name is rejected in review

- **WHEN** an event type is not suffixed with `IntegrationEvent` or is placed outside `Events.<Module>`
- **THEN** it MUST be corrected to follow the naming and namespace convention before merge

### Requirement: Topic declaration via TopicName attribute

Each integration event type SHALL declare the Dapr pub/sub topic it maps to using a `[TopicName("name-of-topic")]` attribute applied to the event type. The publisher and any subscriber tooling SHALL use this attribute's value as the topic name.

#### Scenario: Event declares its topic

- **WHEN** `WeatherChangedIntegrationEvent` is decorated with `[TopicName("weather-changed")]`
- **THEN** publishing that event publishes to the `weather-changed` topic

#### Scenario: Missing topic attribute fails fast

- **WHEN** an integration event without a `[TopicName]` attribute is published
- **THEN** the publisher SHALL throw a clear error identifying the offending event type rather than publishing to an unknown/empty topic

### Requirement: Publisher registration extension method

The library SHALL expose a single extension method, `AddBoogaBoosterIntegrationMessages()`, that registers a publisher service into the dependency injection container. After calling it, a module SHALL be able to inject the publisher and publish any integration event.

#### Scenario: Registration wires up the publisher

- **WHEN** a host calls `AddBoogaBoosterIntegrationMessages()` during startup
- **THEN** the integration-message publisher service is resolvable from DI

#### Scenario: Publishing an event

- **WHEN** a component injects the publisher and publishes a `WeatherChangedIntegrationEvent`
- **THEN** the publisher resolves the topic from the event's `[TopicName]` attribute and publishes it through Dapr pub/sub to the configured broker

### Requirement: Minimal-API subscriber pattern with Dapr WithTopic

Consumers SHALL subscribe to integration events by declaring a minimal API endpoint annotated with Dapr's `WithTopic("topic-name")`, using the same topic name declared on the event's `[TopicName]` attribute. Subscriptions SHALL NOT be defined through static Dapr subscription YAML.

#### Scenario: Endpoint subscribes to a topic

- **WHEN** a module maps a minimal API endpoint that handles `WeatherChangedIntegrationEvent` and annotates it with `WithTopic("weather-changed")`
- **THEN** Dapr delivers messages published to `weather-changed` to that endpoint as the strongly-typed event

#### Scenario: Topic names stay consistent between publisher and subscriber

- **WHEN** a subscriber's `WithTopic(...)` value differs from the event's `[TopicName]` value
- **THEN** this is treated as a defect because the subscriber will not receive the published messages
