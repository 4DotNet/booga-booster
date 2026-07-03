## Context

`FourDotnet.BoogaBooster.App` is an Angular 22 standalone app (signals, OnPush, reactive forms; Vitest for tests) with empty routes and only the default `App` shell. The backend modules (`Controller`, `DigitalTwin`, `Queue`, `Weather`) are empty scaffolds, so there is **no live telemetry or command API yet**. This change delivers the operator's landing page — a visualization plus control and telemetry panels — driven by a service that today simulates data but is shaped to be swapped for a real feed later.

The ride domain: one **central mill** rotating **16 gondolas**; each gondola sits on a **hub** that spins independently (hub speeds vary by load) and holds several **seats**. Operators drive the mill motor and hub motors (power + direction) and monitor security (per-seat restraint), rotation speeds, and per-gondola g-forces.

## Goals / Non-Goals

**Goals:**
- A responsive three-column dashboard (controls | visualization | telemetry) as the default route.
- Clean separation between a single source of truth for ride state/commands and the presentational panels.
- Signal-based reactive data flow so panels update as telemetry changes, with OnPush throughout.
- Accessibility to WCAG AA / AXE-clean, including keyboard-operable controls and non-color-only status.
- A data layer behind an interface so a real backend feed can replace the simulator without touching components.

**Non-Goals:**
- Any backend implementation, real transport (SignalR/HTTP), or persistence.
- Authentication, multi-user, or historical/charting telemetry.
- Physically accurate ride simulation — simulated values only need to be plausible and demonstrate live updates.
- Theming/branding polish beyond an appealing baseline.

## Decisions

### Feature structure
A `ride-dashboard` feature under `src/app/ride-dashboard/`, lazy-loaded via `loadComponent` from `app.routes.ts` (per the Angular guide's lazy-loading rule). The container composes standalone panel components:
- `status-summary` (left top), `operation-controls` (left)
- `ride-visualization` (center)
- `security-panel`, `speed-panel`, `gondola-panel` (right)

Each panel is small, single-responsibility, OnPush, and takes its data via `input()` signals; controls emit intent via `output()` or by calling the service. *Alternative considered:* one monolithic dashboard component — rejected for testability and the CLAUDE.md "small, focused components" rule.

### State: a `RideStateService` (`providedIn: 'root'`)
A single service holds ride state in signals and exposes:
- Read: `state` (running/stopped/emergency), `occupiedSeats`, `securityState`, `mill` (power/direction/speed), `hubs[]` (per-hub speed/power/direction), `gondolas[]` (seats + vertical/lateral g-force), derived via `computed()`.
- Write: `setMillPower`, `setHubPower`, `setMillDirection`, `setHubDirection` — clamp/validate then update state (and, later, forward to the backend).

Data source sits behind a `RideTelemetrySource` interface with a `SimulatedRideTelemetrySource` implementation provided by an injection token. The simulator advances values on a timer (e.g. `interval` from rxjs or a signal-driven tick) to prove live updates. *Alternative considered:* NgRx/component store — rejected as overkill; native signals satisfy the state needs and match repo conventions.

### Models (shared)
Plain TypeScript types/enums under `ride-dashboard/models/`: `RideState`, `MotorDirection` (`forward`/`reverse`), `Mill`, `Hub`, `Gondola`, `Seat` (`empty`/`occupied-unsecured`/`secured`), `GForce { vertical; lateral }`, and command payloads. Keeps components and service strictly typed and gives the future backend a contract to match.

### Controls
Reactive forms for the sliders/toggles. Sliders use native `<input type="range">` (keyboard + `aria-valuenow` for free) with a visible value label; direction toggles are `<button aria-pressed>` or a switch-role control with an accessible name naming the motor. Values clamp to 0–100 before emitting commands.

### Visualization
An inline SVG (16 gondolas positioned around a central hub) animated via CSS transform driven by the mill's speed/direction signal — no canvas, no external chart lib. Because every datum it shows is also in the text panels, the SVG is marked `aria-hidden` (decorative) to avoid redundant/unlabeled graphics for screen-reader users. *Alternative considered:* canvas/WebGL — unnecessary for 16 elements and worse for a11y.

### Layout
CSS grid: three columns on ≥1024px (rails auto-width, center `1fr`), collapsing to a single stacked column below that, with each rail scrolling independently if tall. No CSS framework dependency.

## Risks / Trade-offs

- **Simulated data may diverge from the eventual backend shape** → Define models now as the contract and keep the source behind an interface so only the source implementation changes.
- **Live-updating regions can spam screen readers** → Use `aria-live="polite"` on telemetry regions and keep the visualization decorative; announce values with units only.
- **Animation performance with continuous ticks** → Drive animation with CSS transforms (GPU-friendly), throttle the simulator tick, and stop animating when mill speed is zero.
- **Slider a11y pitfalls** → Prefer native range inputs; if a custom control is needed it must fully implement the slider ARIA pattern and pass AXE.

## Open Questions

- Update cadence / units for the simulator (rpm for rotation, g for forces) — pick sensible defaults; confirm with product later.
- Seat count per gondola — assume a fixed count (e.g. 4) unless specified; make it a model constant so it is easy to change.
- Whether hub power/direction is per-hub or a single group control — proposal treats hubs as one group; revisit if individual hub control is later required.
