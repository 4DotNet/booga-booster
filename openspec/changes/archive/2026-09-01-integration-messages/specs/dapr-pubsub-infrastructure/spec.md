## ADDED Requirements

### Requirement: Dapr pub/sub configured via Aspire Community Toolkit, not YAML

The Dapr pub/sub component SHALL be configured programmatically in the Aspire AppHost (`AppHost.cs`) using the Aspire Community Toolkit for Dapr (`CommunityToolkit.Aspire.Hosting.Dapr`). The system SHALL NOT configure the Dapr pub/sub component through static Dapr component YAML files.

#### Scenario: Component defined in AppHost code

- **WHEN** the AppHost is inspected
- **THEN** the pub/sub component is registered via the toolkit's `AddDaprComponent(...)`/`AddDaprPubSub(...)` API with metadata set in code
- **AND** there is no `components/*.yaml` file defining the pub/sub component

#### Scenario: RabbitMQ backs the component

- **WHEN** the Dapr pub/sub component is registered
- **THEN** it is of type `pubsub.rabbitmq` and its connection metadata points at the Aspire-managed RabbitMQ resource

### Requirement: RabbitMQ broker provisioned by Aspire

The AppHost SHALL provision RabbitMQ as an Aspire-managed resource that acts as the Dapr pub/sub broker, with credentials supplied as Aspire parameters/secrets and the management plugin exposed for local diagnostics.

#### Scenario: RabbitMQ resource is available before dependents start

- **WHEN** the distributed application starts
- **THEN** RabbitMQ starts and dependent projects/components `WaitFor` it before initializing

#### Scenario: Management plugin exposed

- **WHEN** RabbitMQ is provisioned locally
- **THEN** its management plugin endpoint is exposed for inspection

### Requirement: Dapr sidecars attached to publishing and subscribing projects

Every project that publishes or subscribes to integration events SHALL be attached to a Dapr sidecar in the AppHost, and that sidecar SHALL reference the pub/sub component so the project can publish to and receive from the broker.

#### Scenario: API project gets a sidecar referencing pub/sub

- **WHEN** `FourDotnet.BoogaBooster.Api` is registered in the AppHost
- **THEN** it is configured with `WithDaprSidecar(...)` and the sidecar references the pub/sub component

#### Scenario: Sidecar waits for the broker

- **WHEN** a project's Dapr sidecar starts
- **THEN** the pub/sub component and its RabbitMQ dependency are ready (via `WaitFor`) before the project begins publishing or subscribing
