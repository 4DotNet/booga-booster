# FourDotnet.BoogaBooster.IntegrationMessages

Central library for **integration events** — the messages modules use to communicate
without referencing each other. Transport is **Dapr pub/sub** over **RabbitMQ**, wired
in the Aspire AppHost with the **Aspire Community Toolkit for Dapr** (no component YAML).

## Adding a new integration event

1. Create a `sealed record` under `Events/<Module>/` in the
   `FourDotnet.BoogaBooster.IntegrationMessages.Events.<Module>` namespace.
2. Suffix the type name with `IntegrationEvent`.
3. Implement the `IIntegrationEvent` marker interface.
4. Decorate it with `[TopicName("<topic>")]` — the topic name is the single source of
   truth for both publishing and subscribing.

```csharp
namespace FourDotnet.BoogaBooster.IntegrationMessages.Events.Weather;

[TopicName("weather-changed")]
public sealed record WeatherChangedIntegrationEvent(
    string Location,
    int TemperatureC,
    string Summary,
    DateTimeOffset ObservedAt) : IIntegrationEvent;
```

## Publishing

Register the publisher once on the host builder, then inject
`IIntegrationEventPublisher` anywhere and call `PublishAsync`. The topic is resolved
from the event's `[TopicName]`.

```csharp
// Program.cs (API host)
builder.AddBoogaBoosterIntegrationMessages();

// In a module endpoint / handler
public async Task Handle(IIntegrationEventPublisher publisher, CancellationToken ct)
{
    await publisher.PublishAsync(
        new WeatherChangedIntegrationEvent("Amsterdam", 21, "Warm", DateTimeOffset.UtcNow),
        ct);
}
```

## Subscribing

Subscribers are **minimal API endpoints** annotated with Dapr's `WithTopic(...)`,
mapped from the owning module's `Map<Module>Endpoints()` extension (ADR-0007). Use the
same topic name declared on the event's `[TopicName]` — the shared
`IntegrationMessagingDefaults.PubSubName` constant names the pub/sub component.

```csharp
group.MapPost("/on-weather-changed", (WeatherChangedIntegrationEvent @event, ILoggerFactory lf) =>
{
    lf.CreateLogger("Weather").LogInformation("Weather changed in {Location}", @event.Location);
    return Results.Ok();
})
.WithTopic(IntegrationMessagingDefaults.PubSubName, "weather-changed");
```

The API host must enable Dapr subscription routing:

```csharp
app.UseCloudEvents();
app.MapSubscribeHandler();
```

## Infrastructure (AppHost)

The Dapr pub/sub component and RabbitMQ are configured **programmatically** in
`FourDotnet.BoogaBooster.Aspire.AppHost/AppHost.cs` via the Aspire Community Toolkit for
Dapr — **never** with static `components/*.yaml`. Every publishing/subscribing project
is given a Dapr sidecar that references the `pubsub` component.

## Prerequisites

- **Docker** — RabbitMQ runs as an Aspire-managed container.
- **Dapr CLI + runtime** — required for the Aspire Dapr sidecars.
- Set the RabbitMQ credentials the AppHost expects (see AppHost parameters
  `rabbitmq-username` / `rabbitmq-password`) via user secrets before running.
