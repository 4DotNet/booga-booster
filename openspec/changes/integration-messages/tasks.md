## 1. Consult style guide & prerequisites

- [ ] 1.1 Query the `4dotnet-csharp-style-guide` MCP server for guidance on project/folder structure (ADR-0004), minimal APIs (ADR-0002), records/value contracts (ADR-0003), and module endpoint extension methods (ADR-0007) before writing any C#
- [ ] 1.2 Confirm the Dapr CLI/runtime is available for local Aspire sidecars and note the prerequisite in the repo README/CLAUDE guidance

## 2. Create the IntegrationMessages shared library

- [ ] 2.1 Create project `src/Shared/FourDotnet.BoogaBooster.IntegrationMessages` targeting `net10.0` and add it to `src/BoogaBooster.slnx` under the Shared folder
- [ ] 2.2 Add the `Dapr.AspNetCore` (and `Dapr.Client`) NuGet package reference to the library
- [ ] 2.3 Implement `TopicNameAttribute` (`[TopicName("name")]`, `AttributeUsage` = Class, sealed) in the library root namespace
- [ ] 2.4 Add a shared constant for the pub/sub component name (e.g. `IntegrationMessagingDefaults.PubSubName = "pubsub"`)
- [ ] 2.5 (Optional per design) Add a minimal marker interface `IIntegrationEvent` to constrain the publisher generic
- [ ] 2.6 Add topic resolution helper that reads `[TopicName]` via reflection with a per-type `ConcurrentDictionary` cache and throws a descriptive error when the attribute is missing

## 3. Publisher service

- [ ] 3.1 Define `IIntegrationEventPublisher` with `Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct)`
- [ ] 3.2 Implement the publisher using `DaprClient.PublishEventAsync(PubSubName, topic, @event, ct)`, resolving `topic` from `[TopicName]`
- [ ] 3.3 Implement the single `AddBoogaBoosterIntegrationMessages()` extension method that registers `AddDaprClient()` and `IIntegrationEventPublisher`
- [ ] 3.4 Add the reference event `Events.Weather.WeatherChangedIntegrationEvent` (sealed record) decorated with `[TopicName("weather-changed")]` to validate the convention

## 4. Aspire AppHost wiring (Community Toolkit for Dapr, no YAML)

- [ ] 4.1 Add NuGet packages `CommunityToolkit.Aspire.Hosting.Dapr` and `Aspire.Hosting.RabbitMQ` to `FourDotnet.BoogaBooster.Aspire.AppHost`
- [ ] 4.2 In `AppHost.cs`, add `rabbitmq-username`/`rabbitmq-password` secret parameters and provision RabbitMQ (`AddRabbitMQ("messaging", user, pass, port: 5672)`) with `.WithManagementPlugin(port: 15672)`
- [ ] 4.3 Build the AMQP connection string via `ReferenceExpression.Create(...)` from the RabbitMQ endpoint and credentials
- [ ] 4.4 Register the Dapr pub/sub component programmatically with `AddDaprComponent("pubsub", "pubsub.rabbitmq")` + `.WithMetadata("connectionString"/"username"/"password", ...)` + `.WaitFor(rabbitmq)` — do NOT create any `components/*.yaml`
- [ ] 4.5 Attach a Dapr sidecar to the API project: `.WithReference(rabbitmq).WaitFor(rabbitmq).WithDaprSidecar(o => o.WithReference(pubSub))`
- [ ] 4.6 Verify no static Dapr component or subscription YAML exists anywhere in the repo

## 5. API host integration (publish + subscribe)

- [ ] 5.1 Reference `FourDotnet.BoogaBooster.IntegrationMessages` from `FourDotnet.BoogaBooster.Api` and add `Dapr.AspNetCore`
- [ ] 5.2 In `Program.cs`, call `builder.AddBoogaBoosterIntegrationMessages()` and enable Dapr subscribe routing (`AddControllers().AddDapr()` equivalent for minimal APIs / `app.MapSubscribeHandler()` and `app.UseCloudEvents()` as required)
- [ ] 5.3 Map a reference subscriber: minimal-API `POST` endpoint handling `WeatherChangedIntegrationEvent` annotated with `.WithTopic(PubSubName, "weather-changed")`, defined in the owning module and exposed via the module's `Map...Endpoints()` extension (ADR-0007)
- [ ] 5.4 Add a reference publish path (e.g. a temporary endpoint or module call) that injects `IIntegrationEventPublisher` and publishes `WeatherChangedIntegrationEvent`

## 6. Tests

- [ ] 6.1 Add an xUnit (`xunit.v3`) test project if not present for the library; use Moq for `DaprClient`/publisher collaborators (no FluentAssertions)
- [ ] 6.2 Test that topic resolution returns the `[TopicName]` value and caches per type
- [ ] 6.3 Test that publishing an event without `[TopicName]` throws a descriptive exception
- [ ] 6.4 Test that the publisher calls `PublishEventAsync` with the resolved pub/sub name, topic, and payload

## 7. End-to-end verification & docs

- [ ] 7.1 Run the AppHost (`dotnet run --project Aspire/FourDotnet.BoogaBooster.Aspire.AppHost`) and confirm RabbitMQ, the Dapr sidecar, and the API start healthy in the Aspire dashboard
- [ ] 7.2 Publish the reference event and confirm the subscriber endpoint receives it (verify via logs / RabbitMQ management UI)
- [ ] 7.3 Document the pattern (how to add a new integration event, `[TopicName]`, publisher usage, and `WithTopic` subscription) in the repo docs/CLAUDE guidance
- [ ] 7.4 Run `dotnet build BoogaBooster.slnx` and `dotnet test BoogaBooster.slnx` and ensure both pass
