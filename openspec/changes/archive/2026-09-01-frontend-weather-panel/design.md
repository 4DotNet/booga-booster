## Context

The Angular app (`FourDotnet.BoogaBooster.App`) renders a `RideDashboard` landing page: a three-column grid (`rail-left` / `center` / `rail-right`) whose left rail stacks `.panel` blocks (`bb-status-summary`, `bb-operation-controls`). Design tokens (`--bb-bg`, `--bb-surface`, `--bb-accent`, `--bb-ok`, `--bb-alert`, `--bb-focus`, …) are declared on the dashboard `:host` and inherit into every panel. State flows through the signal-based `RideStateService`, which reads a `RideTelemetrySource` behind an `InjectionToken` seam (a simulated implementation today; a real feed later). Components are standalone, `OnPush`, signal-first, `inject()`-based, native control flow, reactive forms, and must pass AXE/WCAG AA (per `src/FourDotnet.BoogaBooster.App/.claude/CLAUDE.md`).

The weather backend already exists: `GET /weather` returns the current conditions, `POST /weather/precipitation` and `POST /weather/strong-wind` disturb it, and the simulation drifts on its own every few seconds. The app has **no** backend wiring yet — no `HttpClient`, no proxy. The app is now an Aspire resource (`AddViteApp`) with a reference to the API, so the API's URL is available to the dev server as a service-discovery env var.

## Goals / Non-Goals

**Goals:**

- A left-rail weather panel showing the live server conditions, kept fresh as the weather drifts.
- Two buttons that disturb the weather (precipitation, strong wind) through the server, with feedback.
- An awesome, data-reactive animated presentation that stays accessible and honors reduced motion.
- Real backend wiring done cleanly (HttpClient + dev proxy + a testable client seam), reusing the app's existing patterns.

**Non-Goals:**

- Changing the weather backend or its contract (consume as-is; normalize enums client-side).
- Real-time push (SignalR/websockets) — polling is sufficient for a drift that ticks every few seconds.
- A production reverse-proxy/hosting story for the SPA (dev proxy only; production hosting is a separate concern).
- Choosing precipitation type in the UI — one precipitation button is enough (default `Rain`); a type picker is a possible later add.

## Decisions

### Reuse the source-seam + state-service pattern

Mirror the ride feature exactly. A `WEATHER_SOURCE` `InjectionToken` fronts a `WeatherSource` interface:

```ts
export interface WeatherSource {
  readonly conditions: Signal<WeatherConditions | null>; // null until first load
  readonly status: Signal<'idle' | 'loading' | 'ready' | 'error'>;
  refresh(): void;
  startPrecipitation(type: PrecipitationType): void;
  startStrongWind(): void;
}
```

`HttpWeatherSource` (the real implementation) owns the `HttpClient` calls and the polling; a `FakeWeatherSource` drives tests deterministically. A `providedIn: 'root'` `WeatherStateService` injects the token and exposes read-model signals the panel binds to (`conditions`, `status`, plus `computed()` niceness label/percent, regime label, precipitation label, a "scene" descriptor for the animation, and `pending` flags for the two actions). Components never touch `HttpClient` directly — same separation the dashboard already uses.

*Alternative considered:* calling `HttpClient` straight from the component — rejected; it breaks the established seam and makes the panel hard to test without HTTP.

### Poll for the drift; refresh eagerly after a disturbance

The weather advances server-side on its own, so the source polls `GET /weather` on an interval (default ~5 s, matching the server tick) using `interval(…).pipe(startWith(0), switchMap(() => get()))` bridged to a signal via `toSignal`, cleaned up with `takeUntilDestroyed`. After a successful disturbance POST (the endpoints return `202 Accepted` with no body), the source triggers an immediate `refresh()` so the change shows without waiting for the next poll. Polling pauses on `document.hidden` is a nice-to-have, not required.

### Typed models that normalize the server enums

The server DTO serializes with Web defaults (camelCase) and, by default, emits the `precipitation`/`regime` enums as **numbers**. The client models use string unions and a normalizer that accepts number **or** string, so the panel is robust whether or not the backend later adds `JsonStringEnumConverter`:

```ts
export type PrecipitationType = 'None' | 'Rain' | 'Snow' | 'Hail';
export type WeatherRegime = 'Calm' | 'Precipitation' | 'StrongWind';
export interface WeatherConditions {
  temperatureCelsius: number; windBeaufort: number; sunshinePercent: number;
  precipitation: PrecipitationType; regime: WeatherRegime; niceWeather: number; // 0..1
}
```

Ordered lookup tables map the numeric enum values (`0=None,1=Rain,2=Snow,3=Hail`; `0=Calm,1=Precipitation,2=StrongWind`) and pass strings through.

### Dev proxy to the Aspire-injected API endpoint

The panel calls **relative** paths under `/api` (`/api/weather`, `/api/weather/precipitation`, `/api/weather/strong-wind`). A `proxy.conf.js` (wired via `angular.json` → `serve.options.proxyConfig`) forwards `^/api` to the API, stripping the `/api` prefix:

```js
const target =
  process.env['services__fourdotnet-boogabooster-api__https__0'] ||
  process.env['services__fourdotnet-boogabooster-api__http__0'] ||
  'https://localhost:7001';
module.exports = [{ context: ['/api'], target, secure: false, changeOrigin: true, pathRewrite: { '^/api': '' } }];
```

`secure: false` accepts the dev certificate; the Aspire env var is read at dev-server start so it works both under the AppHost and standalone (via the fallback). Using a same-origin `/api` prefix avoids CORS and keeps the client free of absolute URLs.

*Alternative considered:* baking an absolute API URL into an Angular `environment.ts` — rejected; it hard-codes a port, reintroduces CORS, and ignores Aspire's dynamic endpoint.

### Awesome, accessible, data-reactive scene

`WeatherPanel` composes a `WeatherConditionsView` (the animated scene + readouts) and `WeatherDisturbanceControls` (the two buttons). The scene is CSS/SVG driven by `computed()` signals:

- Sky gradient interpolates with `niceWeather` and `regime` (bright/warm when nice → cool/grey/stormy when not).
- A sun element glows with `sunshinePercent`; clouds drift in; **rain/snow/hail** particle layers appear while `regime === 'Precipitation'` (glyph by type); **wind streaks** and faster motion during `StrongWind`.
- A niceness meter (0–100%) and bold temperature/wind/sunshine readouts, using `--bb-ok`/`--bb-alert` for good/bad.

Accessibility: the readouts live in an `aria-live="polite"` region summarizing the conditions in words ("20 °C, light breeze, sunny, calm — pleasant"); all animated/decorative SVG is `aria-hidden="true"` with no text in it; every animation is wrapped in `@media (prefers-reduced-motion: reduce)` to stop. Buttons are real `<button>`s with clear labels, disabled + `aria-busy` while their request is pending, and a status message on failure. Contrast is checked against the animated backdrop (text sits on a scrim, not raw gradient).

### Placement

A new `.panel` in `rail-left` after operation controls in `ride-dashboard.html`, importing `WeatherPanel` into `RideDashboard`. The weather feature lives under `src/app/weather/` (its own bounded concern, separate from ride telemetry) and is registered in `app.config.ts` (`provideHttpClient`, `{ provide: WEATHER_SOURCE, useExisting: HttpWeatherSource }`).

## Risks / Trade-offs

- **Server emits numeric enums; UI expects labels** → the normalizer accepts number or string, with lookup tables; a unit test pins both shapes.
- **Dev-cert HTTPS breaks the proxy** → `secure: false` in `proxy.conf.js`; documented fallback target for standalone `ng serve`.
- **Aspire env var name contains hyphens** → read via bracket access `process.env['services__fourdotnet-boogabooster-api__https__0']`, not dotted access.
- **Polling churn / requests after teardown** → single shared interval in the source, `takeUntilDestroyed`, and `toSignal` with an initial value; errors set `status='error'` without tearing down the stream (next tick recovers).
- **"Awesome" animation vs. accessibility/perf** → decorative layers are `aria-hidden`, fully disabled under `prefers-reduced-motion`, and use transform/opacity only (no layout thrash); text sits on a contrast scrim.
- **Disturbance spam** → buttons disable while pending and re-enable on completion; the server itself is idempotent enough (re-triggering just resets the event), so no client-side debounce beyond the pending guard.
- **First backend dependency in the app** → the panel degrades gracefully to an "unavailable" state (not a crash) when the API is down, so the rest of the dashboard is unaffected.
