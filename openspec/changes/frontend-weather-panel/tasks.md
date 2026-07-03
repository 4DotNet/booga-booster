## 1. Backend wiring (weather-api-client)

- [x] 1.1 Add `provideHttpClient(withFetch())` to `app.config.ts`
- [x] 1.2 Create `proxy.conf.js` forwarding `^/api` → the API from `services__fourdotnet-boogabooster-api__https__0` / `__http__0` (localhost fallback, `secure: false`, `changeOrigin: true`, strip `/api`)
- [x] 1.3 Wire the proxy into `angular.json` (`serve.options.proxyConfig`)

## 2. Weather models and API client (weather-api-client)

- [x] 2.1 Add `src/app/weather/models/weather.models.ts`: `PrecipitationType`, `WeatherRegime` string unions and `WeatherConditions` interface
- [x] 2.2 Add enum normalizers that accept numeric or string `precipitation`/`regime` and map to labels
- [x] 2.3 Define the `WeatherSource` interface and `WEATHER_SOURCE` injection token (conditions signal, status signal, `refresh`, `startPrecipitation`, `startStrongWind`)
- [x] 2.4 Implement `HttpWeatherSource`: `GET /api/weather` polling (~5 s) bridged to a signal with `takeUntilDestroyed`; `POST /api/weather/precipitation` and `POST /api/weather/strong-wind`; eager `refresh()` after a successful disturbance; set `status` on load/error
- [x] 2.5 Add a `FakeWeatherSource` for tests (in-memory conditions, controllable status, records disturbance calls)
- [x] 2.6 Register `{ provide: WEATHER_SOURCE, useExisting: HttpWeatherSource }` in `app.config.ts`

## 3. Weather state service (weather-api-client)

- [x] 3.1 Add `WeatherStateService` (`providedIn: 'root'`) injecting `WEATHER_SOURCE`
- [x] 3.2 Expose read-model signals: `conditions`, `status`, and `computed()` niceness percent/label, regime label, precipitation label, and a "scene" descriptor for the animation
- [x] 3.3 Expose `pending` flags per disturbance and `startPrecipitation(type)`/`startStrongWind()` passthroughs that manage pending state and surface failures

## 4. Weather panel display (weather-panel-display)

- [x] 4.1 Add `WeatherConditionsView` component: readouts for temperature, wind (Beaufort + descriptor), sunshine, precipitation, regime, and a nice-weather meter (use `--bb-ok`/`--bb-alert`)
- [x] 4.2 Build the animated scene (CSS/SVG) driven by `computed()` signals: sky gradient by regime + niceness, sun glow by sunshine, drifting clouds, rain/snow/hail particles during precipitation, wind streaks during strong wind
- [x] 4.3 Put the conditions in an `aria-live="polite"` region as a worded summary; mark the animated scene `aria-hidden="true"`
- [x] 4.4 Disable all animation under `@media (prefers-reduced-motion: reduce)`; use transform/opacity only; keep text on a contrast scrim
- [x] 4.5 Add a graceful unavailable/loading state when `status` is not `ready`

## 5. Disturbance controls (weather-disturbance-controls)

- [x] 5.1 Add `WeatherDisturbanceControls` component: two real `<button>`s — "Start precipitation" (default `Rain`) and "Summon strong wind"
- [x] 5.2 Wire buttons to `WeatherStateService.startPrecipitation('Rain')` / `startStrongWind()`
- [x] 5.3 Disable + `aria-busy` the triggering button while its request is pending; re-enable on completion
- [x] 5.4 Show a failure message on error without breaking the panel; ensure visible focus and accessible names

## 6. Compose and mount (weather-panel-display)

- [x] 6.1 Add `WeatherPanel` composing `WeatherConditionsView` + `WeatherDisturbanceControls`
- [x] 6.2 Import `WeatherPanel` into `RideDashboard` and add a `.panel` block in `rail-left` (after operation controls) in `ride-dashboard.html`

## 7. Tests (Vitest + axe)

- [x] 7.1 API client/source: polling fetches `GET /weather`, disturbances POST the right paths/bodies and refresh, teardown stops polling (mock `HttpClient` / `HttpTestingController`)
- [x] 7.2 Enum normalization: numeric and string `precipitation`/`regime` both map to labels
- [x] 7.3 State service: derived niceness/labels/scene and pending/failure behavior (via `FakeWeatherSource`)
- [x] 7.4 Panel display: renders readouts, reflects updates, shows unavailable state on error
- [x] 7.5 Disturbance controls: click triggers the source calls, pending disables the button, failure surfaces a message
- [x] 7.6 a11y spec: the weather panel passes axe with no violations

## 8. Verification

- [x] 8.1 `npm run build` and `npm test` pass (no `any`, strict types, no `standalone: true`, no `@HostBinding`/`@HostListener`, no `ngClass`/`ngStyle`)
- [ ] 8.2 Run under the Aspire AppHost and confirm the panel loads live weather, drifts over time, and both buttons visibly disturb it
- [ ] 8.3 Manually verify keyboard operation, reduced-motion behavior, and contrast on the animated backdrop
