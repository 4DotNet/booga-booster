import { ChangeDetectionStrategy, Component, inject } from '@angular/core';

import { WeatherStateService } from '../state/weather-state.service';

/**
 * The two operator actions that disturb the weather: start precipitation and
 * summon strong wind. Each dispatches through the weather state service, shows a
 * pending state on the triggering button, and surfaces a failure without
 * breaking the panel.
 */
@Component({
  selector: 'bb-weather-disturbance-controls',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="disturb" aria-label="Disturb the weather">
      <div class="buttons">
        <button
          type="button"
          class="disturb-btn"
          data-action="precipitation"
          aria-label="Start precipitation"
          [disabled]="weather.precipitationPending()"
          [attr.aria-busy]="weather.precipitationPending()"
          (click)="startPrecipitation()"
        >
          <span class="glyph" aria-hidden="true">🌧</span>
          <span>{{ weather.precipitationPending() ? 'Starting…' : 'Start precipitation' }}</span>
        </button>

        <button
          type="button"
          class="disturb-btn"
          data-action="strong-wind"
          aria-label="Summon strong wind"
          [disabled]="weather.strongWindPending()"
          [attr.aria-busy]="weather.strongWindPending()"
          (click)="startStrongWind()"
        >
          <span class="glyph" aria-hidden="true">🌬</span>
          <span>{{ weather.strongWindPending() ? 'Summoning…' : 'Summon strong wind' }}</span>
        </button>
      </div>

      @if (weather.errorMessage(); as error) {
        <p class="error" role="alert">{{ error }}</p>
      }
    </section>
  `,
  styles: `
    .disturb {
      display: grid;
      gap: 0.6rem;
    }
    .buttons {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 0.6rem;
    }
    .disturb-btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: 0.4rem;
      padding: 0.55rem 0.7rem;
      border-radius: 0.6rem;
      border: 1px solid var(--bb-border);
      background: var(--bb-surface-2);
      color: inherit;
      font: inherit;
      font-weight: 600;
      cursor: pointer;
      transition: border-color 0.15s ease, background 0.15s ease, transform 0.05s ease;
    }
    .disturb-btn .glyph {
      font-size: 1.1rem;
      line-height: 1;
    }
    .disturb-btn:hover:not(:disabled) {
      border-color: var(--bb-accent);
    }
    .disturb-btn:active:not(:disabled) {
      transform: translateY(1px);
    }
    .disturb-btn:focus-visible {
      outline: 2px solid var(--bb-focus);
      outline-offset: 2px;
    }
    .disturb-btn:disabled {
      opacity: 0.65;
      cursor: progress;
    }
    .error {
      margin: 0;
      color: var(--bb-alert);
      font-size: 0.85rem;
    }
  `,
})
export class WeatherDisturbanceControls {
  protected readonly weather = inject(WeatherStateService);

  protected startPrecipitation(): void {
    void this.weather.startPrecipitation('Rain');
  }

  protected startStrongWind(): void {
    void this.weather.startStrongWind();
  }
}
