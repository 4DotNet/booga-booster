import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { WeatherStatus } from '../data/weather-source';
import {
  WeatherConditions,
  formatTemperature,
  nicenessLabel,
  precipitationLabel,
  regimeLabel,
  sceneFor,
  summaryText,
  windDescription,
} from '../models/weather.models';

/**
 * Presentational weather display: an animated scene that reacts to the live
 * data, plus worded readouts and a nice-weather meter. The scene is decorative
 * (`aria-hidden`); the conditions are conveyed as text in a polite live region,
 * and all motion is disabled under `prefers-reduced-motion`.
 */
@Component({
  selector: 'bb-weather-conditions-view',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="weather" aria-labelledby="weather-heading">
      <h2 id="weather-heading">Weather</h2>

      @if (conditions(); as c) {
        <div
          class="scene"
          aria-hidden="true"
          [attr.data-kind]="scene().kind"
          [style.--sun]="scene().sunshinePercent / 100"
          [style.--nice]="scene().niceWeather"
        >
          <span class="sun"></span>
          <span class="cloud cloud-a"></span>
          <span class="cloud cloud-b"></span>

          @if (scene().kind === 'rain' || scene().kind === 'snow' || scene().kind === 'hail') {
            <span class="particles" [attr.data-kind]="scene().kind">
              @for (p of particles; track p) {
                <span class="particle" [style.--i]="p"></span>
              }
            </span>
          }

          @if (scene().kind === 'wind') {
            <span class="streaks">
              @for (s of streaks; track s) {
                <span class="streak" [style.--i]="s"></span>
              }
            </span>
          }
        </div>

        <p class="summary" role="status" aria-live="polite">{{ summary() }}</p>

        <dl class="readouts">
          <div class="readout">
            <dt>Temperature</dt>
            <dd>{{ temperature() }}</dd>
          </div>
          <div class="readout">
            <dt>Wind</dt>
            <dd>{{ c.windBeaufort }} bft · {{ wind() }}</dd>
          </div>
          <div class="readout">
            <dt>Sunshine</dt>
            <dd>{{ c.sunshinePercent }}%</dd>
          </div>
          <div class="readout">
            <dt>Precipitation</dt>
            <dd>{{ precipitation() }}</dd>
          </div>
          <div class="readout">
            <dt>Regime</dt>
            <dd>{{ regime() }}</dd>
          </div>
        </dl>

        <div class="niceness">
          <div class="niceness-head">
            <span>Nice weather</span>
            <span class="niceness-value" [attr.data-band]="nicenessBand()">
              {{ nicenessPercent() }}% · {{ niceness() }}
            </span>
          </div>
          <div
            class="meter"
            role="meter"
            aria-valuemin="0"
            aria-valuemax="100"
            [attr.aria-valuenow]="nicenessPercent()"
            [attr.aria-valuetext]="niceness()"
            aria-label="Nice weather indicator"
          >
            <span
              class="meter-fill"
              [attr.data-band]="nicenessBand()"
              [style.width.%]="nicenessPercent()"
            ></span>
          </div>
        </div>
      } @else {
        <p class="unavailable" role="status" aria-live="polite">
          {{ status() === 'error' ? 'Weather is currently unavailable.' : 'Loading weather…' }}
        </p>
      }
    </section>
  `,
  styleUrl: './weather-conditions-view.scss',
})
export class WeatherConditionsView {
  readonly conditions = input.required<WeatherConditions | null>();
  readonly status = input.required<WeatherStatus>();

  protected readonly particles = Array.from({ length: 14 }, (_, i) => i);
  protected readonly streaks = Array.from({ length: 5 }, (_, i) => i);

  protected readonly scene = computed(() => sceneFor(this.conditions()));
  protected readonly summary = computed(() => summaryText(this.conditions()));
  protected readonly nicenessPercent = computed(() =>
    Math.round(Math.min(Math.max(this.conditions()?.niceWeather ?? 0, 0), 1) * 100),
  );
  protected readonly niceness = computed(() => nicenessLabel(this.conditions()?.niceWeather ?? 0));
  protected readonly nicenessBand = computed(() => {
    const percent = this.nicenessPercent();
    if (percent >= 80) {
      return 'good';
    }
    if (percent >= 50) {
      return 'fair';
    }
    return 'bad';
  });

  protected readonly temperature = computed(() =>
    formatTemperature(this.conditions()?.temperatureCelsius ?? 0),
  );
  protected readonly wind = computed(() => windDescription(this.conditions()?.windBeaufort ?? 0));
  protected readonly precipitation = computed(() =>
    precipitationLabel(this.conditions()?.precipitation ?? 'None'),
  );
  protected readonly regime = computed(() => regimeLabel(this.conditions()?.regime ?? 'Calm'));
}
