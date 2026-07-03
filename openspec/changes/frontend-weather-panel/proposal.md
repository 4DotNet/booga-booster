## Why

The ride dashboard shows the ride and its telemetry, but the park's weather — which already runs as a live, self-driving simulation on the server (`GET /weather`, and the precipitation/strong-wind endpoints) — is invisible to the operator. Surfacing the weather, and letting the operator poke it, makes the world feel alive and gives the dashboard a reason to talk to the backend for the first time.

## What Changes

- Add a **weather panel** to the dashboard's **left control column** (below the existing status and operation controls) that shows the current conditions — temperature, wind (Beaufort), sunshine, precipitation, the active regime, and the "nice weather" indicator.
- **Fetch the current weather from the server** and keep it fresh by polling, so the panel reflects the weather's autonomous lifecycle as it drifts (by default nothing is wrong — the weather just does its thing).
- Add **two disturbance buttons** on the panel: one to start **precipitation** and one to summon **strong wind**, each calling the corresponding server endpoint, with pending/feedback state and an immediate refresh afterward.
- Make it **look awesome**: an animated weather scene that reacts to the live data (sky/gradient that shifts with the regime and niceness, sun glow, drifting clouds, rain/snow/hail particles while precipitating, wind streaks during strong wind), plus a clear niceness meter and bold readouts — all respecting `prefers-reduced-motion`.
- Introduce the app's **first backend wiring**: provide `HttpClient`, add a dev **proxy** that forwards weather API calls to the Aspire-managed API, and a typed weather API client behind a data-source seam (mirroring the existing telemetry-source pattern) so it stays testable.

## Capabilities

### New Capabilities

- `weather-api-client`: The frontend's connection to the weather backend — `HttpClient` provisioning, the dev proxy that targets the Aspire-injected API endpoint, typed weather models, and a `WeatherSource`/`WeatherApiService` seam that fetches the current conditions (with polling) and posts the precipitation/strong-wind disturbances.
- `weather-panel-display`: The left-column weather panel that presents the current conditions and an awesome, data-reactive animated scene, exposed accessibly (a polite live region announcing the conditions, decorative animation hidden from assistive tech, reduced-motion honored).
- `weather-disturbance-controls`: The two buttons that let the operator disturb the weather (precipitation, strong wind) via the server, including pending state, success/failure feedback, and an immediate refresh of the displayed conditions.

### Modified Capabilities

<!-- None — this adds a panel to the existing dashboard shell without changing its requirements. -->

## Impact

- **Code**: new `src/app/weather/` feature — typed models, `WEATHER_SOURCE` token + HTTP implementation + a fake for tests, a `WeatherStateService`, and the `WeatherPanel` (composing a conditions view and the disturbance controls). `ride-dashboard.ts`/`ride-dashboard.html` gain the panel in the left rail.
- **App config**: `app.config.ts` adds `provideHttpClient(withFetch())`.
- **Dev/build**: new `proxy.conf.js`; `angular.json` `serve` gains `proxyConfig`. The proxy targets the API via the Aspire service-discovery env var (`services__fourdotnet-boogabooster-api__https__0` / `__http__0`) with a localhost fallback, `secure: false` for the dev certificate.
- **Backend contract (consumed, not changed)**: `GET /weather` → `WeatherConditionDto` (`temperatureCelsius`, `windBeaufort`, `sunshinePercent`, `precipitation`, `regime`, `niceWeather`); `POST /weather/precipitation` (`{ type: "Rain" | "Snow" | "Hail" }`); `POST /weather/strong-wind`. The client normalizes the `precipitation`/`regime` enums whether the server emits them as numbers (default) or strings.
- **Accessibility**: must pass AXE / WCAG AA — buttons labeled and operable, conditions in an `aria-live="polite"` region, animation `aria-hidden` and disabled under `prefers-reduced-motion`, sufficient contrast on the animated backdrop.
- **Tests**: Vitest specs for the API client/source and state service, the panel display, the disturbance controls, and an axe a11y spec for the panel.
