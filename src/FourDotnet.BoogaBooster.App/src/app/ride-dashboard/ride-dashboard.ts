import { ChangeDetectionStrategy, Component, inject } from '@angular/core';

import { OperationControls } from './controls/operation-controls';
import { GondolaPanel } from './panels/gondola-panel/gondola-panel';
import { SecurityPanel } from './panels/security-panel/security-panel';
import { SpeedPanel } from './panels/speed-panel/speed-panel';
import { StatusSummary } from './panels/status-summary/status-summary';
import { RideStateService } from './state/ride-state.service';
import { RideVisualization } from './visualization/ride-visualization';
import { WeatherPanel } from '../weather/weather-panel/weather-panel';

/**
 * Landing-page shell for the ride operator. A three-column dashboard —
 * controls rail, central visualization, telemetry rail — that projects the
 * ride-state service's signals into small, single-responsibility panels.
 */
@Component({
  selector: 'bb-ride-dashboard',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    StatusSummary,
    OperationControls,
    RideVisualization,
    SecurityPanel,
    SpeedPanel,
    GondolaPanel,
    WeatherPanel,
  ],
  templateUrl: './ride-dashboard.html',
  styleUrl: './ride-dashboard.scss',
})
export class RideDashboard {
  protected readonly ride = inject(RideStateService);
}
