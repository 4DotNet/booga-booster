import { ChangeDetectionStrategy, Component, inject } from '@angular/core';

import { WeatherStateService } from '../state/weather-state.service';
import { WeatherConditionsView } from './weather-conditions-view';
import { WeatherDisturbanceControls } from './weather-disturbance-controls';

/**
 * Left-rail weather panel. Composes the animated conditions view with the
 * disturbance controls, projecting the weather state service's signals into the
 * presentational view.
 */
@Component({
  selector: 'bb-weather-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [WeatherConditionsView, WeatherDisturbanceControls],
  template: `
    <bb-weather-conditions-view [conditions]="weather.conditions()" [status]="weather.status()" />
    <bb-weather-disturbance-controls />
  `,
  styles: `
    :host {
      display: grid;
      gap: 1rem;
    }
  `,
})
export class WeatherPanel {
  protected readonly weather = inject(WeatherStateService);
}
