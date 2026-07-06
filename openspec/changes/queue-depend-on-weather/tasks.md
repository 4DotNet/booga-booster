## 1. Weather-influence state

- [x] 1.1 Add an `IWeatherInfluence` abstraction and thread-safe singleton `WeatherInfluence` in the Queue module that holds the current multiplier as a single `double` with atomic read/write (e.g. `Interlocked`/lock).
- [x] 1.2 Have it start at the configured neutral default and expose `Update(float niceWeather)` that clamps the incoming value into `[0, 1]` before storing, and a `Current`/`GetMultiplier()` read accessor.

## 2. Configuration

- [x] 2.1 Extend `QueueModuleOptions` with the multiplier-response settings: a neutral pre-event default, a suppression curve/exponent for low `NiceWeather`, and an optional ceiling multiplier for very nice weather.
- [x] 2.2 Choose defaults so behavior before any weather event matches today's base rate, and document each new option with XML doc comments consistent with the existing options.

## 3. Multiplier planning logic

- [x] 3.1 Add a pure `Scale`/`ApplyWeather` helper (in `ArrivalPlanner` or an adjacent static helper) mapping `(baseCount, niceWeather, options)` to a non-negative scaled integer, monotonic in `niceWeather`, using a deterministic rounding rule that floors `NiceWeather = 0` to exactly `0`.
- [x] 3.2 Ensure the scaled count is only the total headcount — group partitioning (`PlanGroupSizes`) and the `MaxQueueLength` cap remain unchanged.

## 4. Subscriber endpoint

- [x] 4.1 Add a Queue subscriber endpoint mapped inside `MapQueueEndpoints`, annotated with Dapr `WithTopic("weather-updated")`, that binds the strongly-typed `WeatherUpdateIntegrationEvent`.
- [x] 4.2 In the handler, call `IWeatherInfluence.Update(event.NiceWeather)` and return a success result so the message is acknowledged; keep the handler free of queue-domain logic.

## 5. Wire the filler

- [x] 5.1 Inject `IWeatherInfluence` into `RideQueueFillerService` and, each cycle, apply the scaling helper to the planned arrival count before partitioning into groups.
- [x] 5.2 Skip enqueuing entirely when the scaled count is `0` (worst weather), preserving the existing capacity/skip logic for the remainder.

## 6. Composition

- [x] 6.1 Register the `WeatherInfluence` singleton in `AddQueueModule` and confirm no Api-host changes are needed (endpoint discovered via existing `MapQueueEndpoints` + `MapSubscribeHandler`).

## 7. Tests

- [x] 7.1 Test that the subscriber handler updates `WeatherInfluence` with the latest clamped value, and that out-of-range and out-of-order events resolve to the newest clamped value.
- [x] 7.2 Test the neutral default: a fill cycle before any event scales by the configured default rather than zero/undefined.
- [x] 7.3 Test the multiplier behavior with seeded `Random` and a fake `TimeProvider`: nice weather yields more arrivals than bad weather, `NiceWeather = 0` yields zero arrivals, and arrivals rise as the indicator increases across cycles.
- [x] 7.4 Test that scaled fills still form valid groups within bounds and still stop at `MaxQueueLength`.

## 8. Verification

- [x] 8.1 Consult the `4dotnet-csharp-style-guide` MCP server for the applicable ADRs (module structure ADR-0007, integration-messaging subscriber pattern, testing) and reconcile the implementation with them.
- [ ] 8.2 Build and test the solution (`dotnet build BoogaBooster.slnx`, `dotnet test BoogaBooster.slnx`) and run via Aspire to confirm weather updates visibly change queue fill rates end-to-end.
