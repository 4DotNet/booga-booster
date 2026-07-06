import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

import {
  RIDE_STATE_BY_INDEX,
  RideState,
  SecurityState,
  rideStateLabel,
} from '../../models/ride.models';

/**
 * Top-left panel: at-a-glance ride state, occupied-seat count, the overall
 * security roll-up, and the lifecycle transition controls. Security and
 * lifecycle state are conveyed by text and icon/attributes, never colour
 * alone. Purely presentational: the current state and the legal transitions
 * both come from the server (`RideLifecycleService`, via `ride-dashboard`),
 * and a click only emits — it never decides legality itself.
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

      <div class="transitions" role="group" aria-labelledby="transitions-heading">
        <h3 id="transitions-heading">Lifecycle transitions</h3>
        <div class="transition-grid">
          @for (candidate of lifecycleStates; track candidate) {
            <button
              type="button"
              class="transition-button"
              [disabled]="!isAvailable(candidate)"
              [attr.aria-current]="isCurrent(candidate) ? 'true' : null"
              (click)="onTransitionClick(candidate)"
            >
              <span>{{ rideStateLabel(candidate) }}</span>
              @if (isCurrent(candidate)) {
                <span class="current-badge">(current)</span>
              }
            </button>
          }
        </div>
      </div>
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
    h3 {
      margin: 0 0 0.5rem;
      font-size: 0.9rem;
      color: var(--bb-muted);
    }
    .metrics {
      margin: 0 0 1rem;
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
    .state[data-state='started'],
    .state[data-state='safe'] {
      color: var(--bb-ok);
    }
    .state[data-state='stopping'],
    .state[data-state='emergency-stop'] {
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
    .transitions {
      display: block;
    }
    .transition-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 0.5rem;
    }
    .transition-button {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.15rem;
      padding: 0.5rem 0.4rem;
      border-radius: 0.5rem;
      border: 1px solid var(--bb-border);
      background: var(--bb-surface-2);
      color: inherit;
      font: inherit;
      font-size: 0.85rem;
      cursor: pointer;
    }
    .transition-button:disabled {
      cursor: not-allowed;
      opacity: 0.5;
    }
    .transition-button[aria-current='true'] {
      border-color: var(--bb-accent);
      font-weight: 700;
    }
    .transition-button:focus-visible {
      outline: 2px solid var(--bb-focus);
      outline-offset: 2px;
    }
    .current-badge {
      font-size: 0.7rem;
      font-weight: 400;
      color: var(--bb-muted);
    }
  `,
})
export class StatusSummary {
  readonly state = input.required<RideState>();
  readonly availableTransitions = input.required<readonly RideState[]>();
  readonly occupiedSeats = input.required<number>();
  readonly securityState = input.required<SecurityState>();

  readonly transition = output<RideState>();

  /** Every lifecycle state, in a fixed order, driving the transition button grid. */
  protected readonly lifecycleStates: readonly RideState[] = RIDE_STATE_BY_INDEX;

  protected readonly stateLabel = computed(() => rideStateLabel(this.state()));
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

  protected readonly rideStateLabel = rideStateLabel;

  /** Whether `candidate` is one of the server's currently-legal transitions. */
  protected isAvailable(candidate: RideState): boolean {
    return this.availableTransitions().includes(candidate);
  }

  /** Whether `candidate` is the ride's current lifecycle state. */
  protected isCurrent(candidate: RideState): boolean {
    return this.state() === candidate;
  }

  /** Emit a transition request; only ever called for a legal, enabled button. */
  protected onTransitionClick(candidate: RideState): void {
    if (this.isAvailable(candidate)) {
      this.transition.emit(candidate);
    }
  }
}
