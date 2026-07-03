import { Injectable, Signal, WritableSignal, computed, inject, signal } from '@angular/core';

import { WEATHER_SOURCE, WeatherStatus } from '../data/weather-source';
import {
  PrecipitationType,
  WeatherConditions,
  WeatherScene,
  nicenessLabel,
  precipitationLabel,
  regimeLabel,
  sceneFor,
  summaryText,
  windDescription,
} from '../models/weather.models';

/**
 * Single source of truth for the weather panel. Projects the injected
 * {@link WeatherSource} into read-model signals the panel binds to, and wraps
 * the disturbance operations with per-action pending state and error reporting.
 */
@Injectable({ providedIn: 'root' })
export class WeatherStateService {
  private readonly source = inject(WEATHER_SOURCE);

  /** Current conditions; `null` until the first successful load. */
  readonly conditions: Signal<WeatherConditions | null> = this.source.conditions;

  /** Current load status of the feed. */
  readonly status: Signal<WeatherStatus> = this.source.status;

  /** Whether conditions are ready to display. */
  readonly isReady = computed(() => this.status() === 'ready' && this.conditions() !== null);

  /** Nice-weather indicator as a 0–100 percentage. */
  readonly nicenessPercent = computed(() =>
    Math.round(clamp01(this.conditions()?.niceWeather ?? 0) * 100),
  );

  /** Qualitative nice-weather label. */
  readonly nicenessLabel = computed(() => nicenessLabel(this.conditions()?.niceWeather ?? 0));

  /** Human-friendly active-regime label. */
  readonly regimeLabel = computed(() => regimeLabel(this.conditions()?.regime ?? 'Calm'));

  /** Human-friendly precipitation label. */
  readonly precipitationLabel = computed(() =>
    precipitationLabel(this.conditions()?.precipitation ?? 'None'),
  );

  /** Human-friendly wind description. */
  readonly windDescription = computed(() => windDescription(this.conditions()?.windBeaufort ?? 0));

  /** Descriptor driving the animated scene. */
  readonly scene: Signal<WeatherScene> = computed(() => sceneFor(this.conditions()));

  /** A worded, one-line summary for a live region. */
  readonly summary = computed(() => summaryText(this.conditions()));

  private readonly precipitationPendingSignal = signal(false);
  private readonly strongWindPendingSignal = signal(false);
  private readonly errorMessageSignal = signal<string | null>(null);

  /** Whether a precipitation request is in flight. */
  readonly precipitationPending = this.precipitationPendingSignal.asReadonly();

  /** Whether a strong-wind request is in flight. */
  readonly strongWindPending = this.strongWindPendingSignal.asReadonly();

  /** The most recent disturbance failure message, or `null`. */
  readonly errorMessage = this.errorMessageSignal.asReadonly();

  /** Force an immediate re-fetch of the current conditions. */
  refresh(): void {
    this.source.refresh();
  }

  /** Start a precipitation event (defaults to rain). */
  async startPrecipitation(type: PrecipitationType = 'Rain'): Promise<void> {
    await this.run(this.precipitationPendingSignal, () => this.source.startPrecipitation(type));
  }

  /** Start a strong-wind event. */
  async startStrongWind(): Promise<void> {
    await this.run(this.strongWindPendingSignal, () => this.source.startStrongWind());
  }

  private async run(pending: WritableSignal<boolean>, action: () => Promise<void>): Promise<void> {
    this.errorMessageSignal.set(null);
    pending.set(true);
    try {
      await action();
    } catch {
      this.errorMessageSignal.set('Could not disturb the weather. Please try again.');
    } finally {
      pending.set(false);
    }
  }
}

function clamp01(value: number): number {
  return Math.min(Math.max(value, 0), 1);
}
