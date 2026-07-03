## Why

The Angular app (`FourDotnet.BoogaBooster.App`) is currently an empty scaffold with no routes and no UI. The ride operator needs a single landing page that both visualizes the amusement ride and lets them operate it — applying power and direction to the motors while monitoring security, speed, and g-force telemetry at a glance.

## What Changes

- Add a full-screen **ride dashboard** as the app's landing page: a three-column layout with a left control rail, a central ride visualization, and a right telemetry rail.
- Add a **central visual representation** of the ride (central mill with 16 gondolas on rotating hubs) that reflects live rotation and occupancy.
- Add a **status summary** panel (top-left) showing ride state (e.g. stopped/running/emergency), occupied-seat count, and overall security state.
- Add **operation controls** (below status, left rail) to drive the central mill motor and the hub motors: a power slider per motor group and a direction (reverse) toggle for both the central mill and the hubs.
- Add a **security telemetry** panel (top-right) showing occupied-seat count and each seat's secured state (restraint closed and locked).
- Add a **speed telemetry** panel (middle-right) showing the main mill rotation speed and each hub's individual rotation speed.
- Add a **gondola telemetry** panel (bottom-right) listing all 16 gondolas with their occupied seats plus vertical (push-back / push-forward) and lateral g-forces.
- Introduce a **ride-state service** that supplies live/simulated telemetry and accepts control commands, so panels stay in sync without a backend (backend wiring is out of scope).

## Capabilities

### New Capabilities
- `ride-dashboard-layout`: The landing-page shell — responsive three-column layout, status-summary panel, and the central ride visualization.
- `ride-operation-controls`: Operator controls for the central mill and hub motors — power sliders and direction toggles that emit control commands.
- `ride-telemetry-panels`: Read-only monitoring panels — per-seat security state, mill and per-hub rotation speeds, and per-gondola occupancy and g-forces.

### Modified Capabilities
<!-- None — no existing specs. -->

## Impact

- **Code**: `src/FourDotnet.BoogaBooster.App` — new route (default `''`), new feature components under `src/app/`, a ride-state service, and shared telemetry/command models. Updates `app.routes.ts` and `app.ts`/`app.html`.
- **Dependencies**: None required beyond the current Angular 22 stack; controls built with reactive forms and native inputs (no new UI library mandated).
- **Backend**: None in this change — the ride-state service uses simulated data behind an interface so it can later be swapped for a real telemetry feed (SignalR/HTTP) from the backend modules.
- **Accessibility**: Must pass AXE / WCAG AA (focus management, contrast, ARIA for sliders/toggles and live-updating telemetry regions).
