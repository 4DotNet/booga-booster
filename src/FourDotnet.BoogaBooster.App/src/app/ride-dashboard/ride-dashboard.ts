import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';

import { OperationControls } from './controls/operation-controls';
import { GondolaPanel } from './panels/gondola-panel/gondola-panel';
import { RiderExperiencePanel } from './panels/rider-experience-panel/rider-experience-panel';
import { SecurityPanel } from './panels/security-panel/security-panel';
import { SpeedPanel } from './panels/speed-panel/speed-panel';
import { StatusSummary } from './panels/status-summary/status-summary';
import { RideLifecycleService } from './state/ride-lifecycle.service';
import { RideStateService } from './state/ride-state.service';
import { RideVisualization } from './visualization/ride-visualization';
import { queuedGuests } from '../queue/models/queue.models';
import { QueuePanel } from '../queue/queue-panel/queue-panel';
import { QueueStateService } from '../queue/state/queue-state.service';
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
    RiderExperiencePanel,
    QueuePanel,
    WeatherPanel,
  ],
  templateUrl: './ride-dashboard.html',
  styleUrl: './ride-dashboard.scss',
})
export class RideDashboard {
  protected readonly ride = inject(RideStateService);
  protected readonly lifecycle = inject(RideLifecycleService);
  private readonly queue = inject(QueueStateService);

  /** Everyone waiting in the queue, front first. */
  protected readonly waitingGuests = computed(() => queuedGuests(this.queue.queue()));
}
