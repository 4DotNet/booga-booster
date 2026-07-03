## 1. Models and data layer

- [x] 1.1 Add `ride-dashboard/models/` types: `RideState`, `MotorDirection`, `Seat`/`SeatState`, `GForce`, `Gondola`, `Hub`, `Mill`, and command payload types
- [x] 1.2 Define a `RideTelemetrySource` interface and an injection token for it
- [x] 1.3 Implement `SimulatedRideTelemetrySource` that advances plausible values on a throttled tick (mill/hub speeds in rpm, g-forces in g, seat/occupancy states)
- [x] 1.4 Implement `RideStateService` (`providedIn: 'root'`) exposing read signals (`state`, `occupiedSeats`, `securityState`, `mill`, `hubs`, `gondolas`) and derived `computed()` values
- [x] 1.5 Add command methods to `RideStateService` (`setMillPower`, `setHubPower`, `setMillDirection`, `setHubDirection`) that clamp power to 0–100 and update state
- [x] 1.6 Unit-test the service: command clamping, direction toggling, and state/computed correctness

## 2. Routing and dashboard shell

- [x] 2.1 Add a default route (`''`) in `app.routes.ts` that lazy-loads the `ride-dashboard` container via `loadComponent`, plus a wildcard redirect to it
- [x] 2.2 Update `app.html`/`app.ts` so the dashboard renders through `RouterOutlet`
- [x] 2.3 Create the `ride-dashboard` container component (OnPush) with the three-column CSS grid layout (controls | visualization | telemetry)
- [x] 2.4 Make the layout responsive: stack to a single column below 1024px with no horizontal overflow and independently scrolling rails
- [x] 2.5 Add landmark structure (main/regions, headings) for the dashboard

## 3. Status summary and visualization (ride-dashboard-layout)

- [x] 3.1 Build `status-summary` panel (left top): ride state, occupied-seat count, overall security state, with non-color-only secured/unsecured indication
- [x] 3.2 Build `ride-visualization` (center): inline SVG of the central mill with 16 gondolas, occupancy shown per gondola
- [x] 3.3 Animate the visualization via CSS transform from the mill speed/direction signal; static when speed is zero
- [x] 3.4 Mark the visualization decorative (`aria-hidden`) since its data is duplicated in text panels
- [x] 3.5 Tests: default-route renders dashboard; summary reflects state; visualization static at zero speed

## 4. Operation controls (ride-operation-controls)

- [x] 4.1 Build `operation-controls` panel (left) using reactive forms
- [x] 4.2 Add central mill power slider (native range, visible value, 0–100) wired to `setMillPower`
- [x] 4.3 Add hub power slider wired to `setHubPower`
- [x] 4.4 Add mill and hub direction toggles (forward/reverse) wired to `setMillDirection`/`setHubDirection`, exposing `aria-pressed` and motor-naming labels
- [x] 4.5 Initialize controls from the service and keep them in sync when state changes elsewhere
- [x] 4.6 Tests: slider emits clamped commands, keyboard adjustment works, toggles emit correct direction and expose state

## 5. Telemetry panels (ride-telemetry-panels)

- [x] 5.1 Build `security-panel` (right top): occupied-seat total and per-seat secured/unsecured/empty state, non-color-only
- [x] 5.2 Build `speed-panel` (right middle): main mill rotation speed plus each hub's individual speed with units, live-updating
- [x] 5.3 Build `gondola-panel` (right bottom): all 16 gondolas, each showing occupied seats and vertical (signed) and lateral (signed) g-forces with units
- [x] 5.4 Wrap live-updating telemetry regions in polite live regions and include units in accessible names
- [x] 5.5 Tests: 16 gondolas rendered, hub speeds shown independently, signed g-forces displayed, seat states correct

## 6. Accessibility and verification

- [x] 6.1 Run an AXE audit against the rendered dashboard and fix any violations
- [x] 6.2 Verify keyboard navigation and focus management across all controls and panels
- [x] 6.3 Run `npm test` (Vitest) and `npm run build`; ensure both pass
