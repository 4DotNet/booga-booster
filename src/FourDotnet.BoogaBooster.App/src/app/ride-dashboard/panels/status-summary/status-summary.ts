import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { RideState, SecurityState } from '../../models/ride.models';

/**
 * Top-left panel: at-a-glance ride state, occupied-seat count and the overall
 * security roll-up. Security is conveyed by text and an icon, never colour alone.
 */
@Component({
  selector: 'bb-status-summary',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="summary" aria-labelledby="summary-heading">
      <h2 id="summary-heading">Status summary</h2>
      <dl class="metrics">
        <div class="metric">
          <dt>Ride state</dt>
          <dd class="state" [attr.data-state]="state()">{{ stateLabel() }}</dd>
        </div>
        <div class="metric">
          <dt>Occupied seats</dt>
          <dd>{{ occupiedSeats() }}</dd>
        </div>
        <div class="metric">
          <dt>Security</dt>
          <dd
            class="security"
            [attr.data-security]="securityState()"
            [attr.aria-label]="securityLabel()"
          >
            <span class="icon" aria-hidden="true">{{ securityIcon() }}</span>
            <span>{{ securityText() }}</span>
          </dd>
        </div>
      </dl>
    </section>
  `,
  styles: `
    .summary {
      display: block;
    }
    h2 {
      margin: 0 0 0.75rem;
      font-size: 1rem;
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
    .state {
      text-transform: capitalize;
    }
    .state[data-state='running'] {
      color: var(--bb-ok);
    }
    .state[data-state='emergency'] {
      color: var(--bb-alert);
    }
    .security {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
    }
    .security[data-security='secured'] {
      color: var(--bb-ok);
    }
    .security[data-security='unsecured'] {
      color: var(--bb-alert);
    }
    .icon {
      font-weight: 700;
    }
  `,
})
export class StatusSummary {
  readonly state = input.required<RideState>();
  readonly occupiedSeats = input.required<number>();
  readonly securityState = input.required<SecurityState>();

  protected readonly stateLabel = computed(() => this.state());
  protected readonly securityText = computed(() =>
    this.securityState() === 'secured' ? 'Secured' : 'Unsecured',
  );
  protected readonly securityIcon = computed(() =>
    this.securityState() === 'secured' ? '✓' : '⚠',
  );
  protected readonly securityLabel = computed(() =>
    this.securityState() === 'secured'
      ? 'All occupied seats secured'
      : 'One or more occupied seats not secured',
  );
}
