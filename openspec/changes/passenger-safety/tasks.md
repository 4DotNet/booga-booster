## 1. Backend — contract & parameters

- [ ] 1.1 Add `StressLevel { Safe, Warning, Failure }` enum (Safe = index 0, ordered worst-last) to `DigitalTwin.Abstractions/StressLevel.cs`
- [ ] 1.2 Add rpm limits to `Domain/RideParameters.cs`: `MillWarnRpm = 15`, `MillSafetyRpm = 18`, `HubWarnRpm = 26`, `HubSafetyRpm = 32`
- [ ] 1.3 Add `StressLevel Stress` to `Abstractions/MillTelemetry.cs` and `Abstractions/HubTelemetry.cs` (with XML doc)

## 2. Backend — domain classification

- [ ] 2.1 Add a pure classifier (warn limit, safety limit, |rpm| → `StressLevel`) — a private static helper reused by mill and hub
- [ ] 2.2 In `Domain/Hub.cs`, expose `StressLevel Stress` from `Math.Abs(RotationalDynamics.ToRpm(_omega))` vs the hub limits; project it in `ToTelemetry()`
- [ ] 2.3 In `Domain/GreatMill.cs`, expose the mill's own `StressLevel Stress` (mill limits) and a ride-wide roll-up `StressLevel OverallStress` = worst of mill + every hub; project the mill's own stress in `ToTelemetry()`

## 3. Backend — automatic safety-mode trip

- [ ] 3.1 In `Ride.Advance`, in the physics branch, after `AdvancePhysics`: when `_state == Started` and `_mill.OverallStress == StressLevel.Failure`, run the emergency-stop entry (cut mill+hub power, engage gondola brakes) and set `_state = EmergencyStop`
- [ ] 3.2 Verify the existing `EmergencyStop → Offloading` settle-at-rest path still runs after an automatic trip (coast-down to rest, brakes engaged)

## 4. Backend — tests (xUnit v3, no FluentAssertions)

- [ ] 4.1 Mill classification: safe at ≤15, warning in (15, 18], failure above 18 (boundary cases included)
- [ ] 4.2 Hub classification: safe at ≤26, warning in (26, 32], failure above 32; negative (reverse) rpm classified on magnitude
- [ ] 4.3 Roll-up: `OverallStress` returns the worst of mill + hubs
- [ ] 4.4 Trip: a `Started` ride driven past a safety limit auto-transitions to `EmergencyStop` on the next `Advance`, with power cut and gondola brakes engaged; no trip while stress is `safe`/`warning`
- [ ] 4.5 Telemetry: `MillTelemetry.Stress` / `HubTelemetry.Stress` reflect the classification

## 5. Frontend — model & mapping

- [ ] 5.1 In `ride-dashboard/models/ride.models.ts` add `StressLevel = 'safe' | 'warning' | 'failure'`
- [ ] 5.2 Add mirrored rpm-limit constants (`MILL_WARN_RPM`, `MILL_SAFETY_RPM`, `HUB_WARN_RPM`, `HUB_SAFETY_RPM`) with "must stay in sync with backend" comments
- [ ] 5.3 Add helpers: `millStressLevel(rpm)`, `hubStressLevel(rpm)`, and `overallStress(mill, hubs)` (worst-wins) using `Math.abs(rpm)`
- [ ] 5.4 Add `stress` to `RideTelemetryMillStreamDto` / `RideTelemetryHubStreamDto` and map it in `mapRideTelemetry` (accept numeric-or-name enum, defaulting to `safe`)

## 6. Frontend — panels & wiring

- [ ] 6.1 `panels/speed-panel`: flag the mill value and each hub value by `warning`/`failure` via a `data-state` attribute plus icon+text (never colour alone), keeping the value's aria-label
- [ ] 6.2 `panels/security-panel`: add a `stress` input and a third "Stress: <level>" row with an icon+text indication for safe/warning/failure
- [ ] 6.3 Expose ride-wide stress on `RideStateService` and wire it into `<bb-security-panel [stress]="…" />` in `ride-dashboard.html`

## 7. Frontend — tests

- [ ] 7.1 Model spec: classification helpers at each boundary + `overallStress` worst-wins + reverse (negative) rpm
- [ ] 7.2 `speed-panel` spec: warning/failure flag rendered for over-limit mill and hub values, indication is not colour-only
- [ ] 7.3 `security-panel` spec: Stress row renders safe/warning/failure with correct icon+text; passes AXE

## 8. Verify

- [ ] 8.1 `dotnet test BoogaBooster.slnx` — backend green, module coverage ≥ 80%
- [ ] 8.2 `npm test` in `FourDotnet.BoogaBooster.App` — frontend green, AXE checks pass
- [ ] 8.3 Manual: drive the mill past 18 rpm and a hub past 32 rpm and confirm the ride auto-stops, brakes engage, the speed panel warns, and the Load & security Stress row reads failure
