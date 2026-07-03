import { signal } from '@angular/core';

import { PrecipitationType, WeatherConditions } from '../models/weather.models';
import { WeatherSource, WeatherStatus } from '../data/weather-source';

/**
 * Deterministic {@link WeatherSource} for tests: no HTTP, no timers. Conditions
 * and status are set directly, and every disturbance call is recorded so specs
 * can assert on what was dispatched. Set {@link failNext} to make the next
 * disturbance reject.
 */
export class FakeWeatherSource implements WeatherSource {
  readonly precipitationCalls: PrecipitationType[] = [];
  strongWindCalls = 0;
  refreshCount = 0;
  failNext = false;

  /** When true, disturbance calls stay pending until {@link release} is called. */
  hold = false;

  private readonly releaseFns: Array<() => void> = [];
  private readonly conditionsSignal = signal<WeatherConditions | null>(null);
  private readonly statusSignal = signal<WeatherStatus>('idle');

  readonly conditions = this.conditionsSignal.asReadonly();
  readonly status = this.statusSignal.asReadonly();

  setConditions(conditions: WeatherConditions | null): void {
    this.conditionsSignal.set(conditions);
  }

  setStatus(status: WeatherStatus): void {
    this.statusSignal.set(status);
  }

  refresh(): void {
    this.refreshCount++;
  }

  /** Resolve every held disturbance call. */
  release(): void {
    const fns = this.releaseFns.splice(0);
    fns.forEach((fn) => fn());
  }

  async startPrecipitation(type: PrecipitationType): Promise<void> {
    this.precipitationCalls.push(type);
    await this.wait();
    this.maybeFail();
  }

  async startStrongWind(): Promise<void> {
    this.strongWindCalls++;
    await this.wait();
    this.maybeFail();
  }

  private async wait(): Promise<void> {
    if (!this.hold) {
      return;
    }
    await new Promise<void>((resolve) => this.releaseFns.push(resolve));
  }

  private maybeFail(): void {
    if (this.failNext) {
      this.failNext = false;
      throw new Error('Disturbance failed');
    }
  }
}

/** Build a conditions snapshot, overriding any fields for a specific scenario. */
export function createConditions(overrides: Partial<WeatherConditions> = {}): WeatherConditions {
  return {
    temperatureCelsius: 20,
    windBeaufort: 2,
    sunshinePercent: 70,
    precipitation: 'None',
    regime: 'Calm',
    niceWeather: 1,
    ...overrides,
  };
}
