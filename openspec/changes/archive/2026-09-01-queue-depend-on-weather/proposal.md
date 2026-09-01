## Why

The ride-queue filler currently enqueues a fixed 4–8 arrivals every cycle regardless of the weather, so the park feels lifeless: bad weather never thins the lines and sunshine never brings the crowds back. The Weather module already publishes a `WeatherUpdateIntegrationEvent` (topic `weather-updated`) carrying a `NiceWeather` indicator in `[0, 1]`, but nothing consumes it. The Queue module should react to that event so arrival volume tracks the weather.

## What Changes

- The Queue module subscribes to the `WeatherUpdateIntegrationEvent` via a minimal-API endpoint annotated with Dapr's `WithTopic("weather-updated")`, following the established integration-messaging subscriber pattern (no static subscription YAML).
- The latest `NiceWeather` value is held in a thread-safe, in-memory, singleton "weather influence" state that the subscriber updates and the background filler reads each cycle. It starts at a configurable neutral default until the first event arrives, so the filler behaves sensibly before any weather is published.
- The background filler multiplies its planned arrival count by the current `NiceWeather` multiplier before enqueuing. With `NiceWeather ≈ 1` the line fills at (or above) the base rate; as it approaches `0`, arrivals fall toward zero — in the worst weather nobody queues; as the weather recovers, arrivals resume.
- The multiplier response is configurable (e.g. how sharply low `NiceWeather` suppresses arrivals, and an optional ceiling for very nice weather), keeping the base bounds and interval from `QueueModuleOptions` intact.
- No change to the `WeatherUpdateIntegrationEvent` contract, the Weather module, or the queue's HTTP read surface.

## Capabilities

### New Capabilities

- `weather-driven-queue-multiplier`: The Queue module's subscription to the weather-update integration event, the in-memory weather-influence state that stores the latest `NiceWeather` indicator, and the rule that turns that indicator into a multiplier applied to each fill cycle's arrival count (down to no arrivals in the worst weather, resuming as it improves), including the neutral pre-event default and configurable response.

### Modified Capabilities

<!-- None — no existing published specs (openspec/specs/ is empty) change their requirements. This change realizes the weather-driven arrival behavior concretely via the integration event. -->

## Impact

- **Modules**: `src/Queue/FourDotnet.BoogaBooster.Queue` — adds a subscriber endpoint (mapped through `MapQueueEndpoints`), a singleton weather-influence state, and multiplier logic wired into `RideQueueFillerService`/`ArrivalPlanner`; extends `QueueModuleOptions` with multiplier-response settings and `AddQueueModule` to register the new state.
- **Integration messaging**: consumes the existing `FourDotnet.BoogaBooster.IntegrationMessages.Events.Weather.WeatherUpdateIntegrationEvent`; relies on the Api host's `UseCloudEvents()` + `MapSubscribeHandler()` and the Api project's Dapr sidecar/pub-sub already provisioned by the AppHost.
- **API host**: no code change — the Queue subscriber endpoint is discovered automatically once `MapQueueEndpoints()` maps it.
- **Testing**: adds Queue tests covering the subscriber updating state, the neutral default before any event, and the multiplier driving arrivals from base rate down to zero and back, using seeded randomness and a fake `TimeProvider`.
- **Dependencies**: no new NuGet packages; `integration-messages` (Dapr pub/sub) and `weather-service` (publisher of `WeatherUpdateIntegrationEvent`) are prerequisites for end-to-end behavior.
