import { provideHttpClient, withFetch } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { HttpQueueSource } from './queue/data/http-queue-source';
import { QUEUE_SOURCE } from './queue/data/queue-source';
import { RIDE_TELEMETRY_SOURCE } from './ride-dashboard/data/ride-telemetry-source';
import { SseRideTelemetrySource } from './ride-dashboard/data/sse-ride-telemetry-source';
import { HttpWeatherSource } from './weather/data/http-weather-source';
import { WEATHER_SOURCE } from './weather/data/weather-source';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withFetch()),
    { provide: RIDE_TELEMETRY_SOURCE, useExisting: SseRideTelemetrySource },
    { provide: WEATHER_SOURCE, useExisting: HttpWeatherSource },
    { provide: QUEUE_SOURCE, useExisting: HttpQueueSource },
  ],
};
