import { ChangeDetectionStrategy, Component, inject } from '@angular/core';

import { QueueStateService } from '../state/queue-state.service';

/**
 * Left-rail queue panel: at-a-glance counts of groups and individual people
 * waiting for the ride. Read-only — there is nothing to command here, only
 * the current counts from the poll. The worded summary lives in a polite
 * live region so operators hear updates as new arrivals land.
 */
@Component({
  selector: 'bb-queue-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="queue" aria-labelledby="queue-heading">
      <h2 id="queue-heading">Queue</h2>

      @if (queue.isReady()) {
        <p class="summary" role="status" aria-live="polite">{{ queue.summary() }}</p>
        <dl class="metrics">
          <div class="metric">
            <dt>Groups queued</dt>
            <dd>{{ queue.groupCount() }}</dd>
          </div>
          <div class="metric">
            <dt>People waiting</dt>
            <dd>{{ queue.peopleWaiting() }}</dd>
          </div>
        </dl>
      } @else {
        <p class="unavailable" role="status" aria-live="polite">
          {{ queue.status() === 'error' ? 'Queue unavailable.' : 'Loading queue…' }}
        </p>
      }
    </section>
  `,
  styles: `
    .queue {
      display: grid;
      gap: 0.6rem;
    }
    h2 {
      margin: 0;
      font-size: 1rem;
    }
    .summary {
      margin: 0;
      color: var(--bb-muted);
    }
    .metrics {
      margin: 0;
      display: grid;
      gap: 0.5rem;
    }
    .metric {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 1rem;
    }
    dt {
      color: var(--bb-muted);
    }
    dd {
      margin: 0;
      font-weight: 600;
      font-variant-numeric: tabular-nums;
    }
    .unavailable {
      margin: 0;
      color: var(--bb-muted);
    }
  `,
})
export class QueuePanel {
  protected readonly queue = inject(QueueStateService);
}
