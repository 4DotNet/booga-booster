import { InjectionToken, Signal } from '@angular/core';

import { PrecipitationType, WeatherConditions } from '../models/weather.models';

/** Load status of the weather feed. */
export type WeatherStatus = 'idle' | 'loading' | 'ready' | 'error';

/**
 * The seam between the weather state service and whatever produces the weather.
 *
 * The HTTP implementation talks to the backend and polls; a fake implementation
 * drives tests. Implementations expose the current conditions and status as
 * signals, poll/refresh on demand, and post disturbances (the returned promise
 * resolves on success and rejects on failure).
 */
export interface WeatherSource {
  /** Current conditions; `null` until the first successful load. */
  readonly conditions: Signal<WeatherConditions | null>;

  /** Current load status of the feed. */
  readonly status: Signal<WeatherStatus>;

  /** Force an immediate re-fetch of the current conditions. */
  refresh(): void;

  /** Start a precipitation event of the given type on the server. */
  startPrecipitation(type: PrecipitationType): Promise<void>;

  /** Start a strong-wind event on the server. */
  startStrongWind(): Promise<void>;
}

/** DI token for the active {@link WeatherSource} implementation. */
export const WEATHER_SOURCE = new InjectionToken<WeatherSource>('WEATHER_SOURCE');
