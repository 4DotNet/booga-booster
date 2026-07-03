import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { Gondola, SeatState } from '../../models/ride.models';

interface SeatView {
  readonly key: string;
  readonly gondolaId: number;
  readonly seatId: number;
  readonly state: SeatState;
  readonly icon: string;
  readonly label: string;
}

/**
 * Top-right panel: total occupied seats plus each seat's secured, unsecured or
 * empty state. Each state has a distinct icon and text label, never colour alone.
 */
@Component({
  selector: 'bb-security-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="security" aria-labelledby="security-heading">
      <h2 id="security-heading">Security</h2>
      <p class="total" aria-live="polite">
        {{ occupiedCount() }} occupied seat{{ occupiedCount() === 1 ? '' : 's' }}
      </p>
      <ul class="seats">
        @for (seat of seats(); track seat.key) {
          <li class="seat" [attr.data-state]="seat.state">
            <span class="icon" aria-hidden="true">{{ seat.icon }}</span>
            <span class="sr-only">{{ seat.label }}</span>
          </li>
        }
      </ul>
    </section>
  `,
  styles: `
    .security {
      display: grid;
      gap: 0.6rem;
    }
    h2 {
      margin: 0;
      font-size: 1rem;
    }
    .total {
      margin: 0;
      font-weight: 600;
    }
    .seats {
      list-style: none;
      margin: 0;
      padding: 0;
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(1.75rem, 1fr));
      gap: 0.3rem;
    }
    .seat {
      display: grid;
      place-items: center;
      aspect-ratio: 1;
      border-radius: 0.35rem;
      border: 1px solid var(--bb-border);
      background: var(--bb-surface-2);
    }
    .seat[data-state='secured'] {
      border-color: var(--bb-ok);
      color: var(--bb-ok);
    }
    .seat[data-state='occupied-unsecured'] {
      border-color: var(--bb-alert);
      color: var(--bb-alert);
    }
    .icon {
      font-size: 0.85rem;
      line-height: 1;
    }
    .sr-only {
      position: absolute;
      width: 1px;
      height: 1px;
      padding: 0;
      margin: -1px;
      overflow: hidden;
      clip: rect(0, 0, 0, 0);
      white-space: nowrap;
      border: 0;
    }
  `,
})
export class SecurityPanel {
  readonly gondolas = input.required<readonly Gondola[]>();

  protected readonly seats = computed<SeatView[]>(() =>
    this.gondolas().flatMap((gondola) =>
      gondola.seats.map((seat) => ({
        key: `${gondola.id}-${seat.id}`,
        gondolaId: gondola.id,
        seatId: seat.id,
        state: seat.state,
        icon: iconFor(seat.state),
        label: `Gondola ${gondola.id} seat ${seat.id}: ${labelFor(seat.state)}`,
      })),
    ),
  );

  protected readonly occupiedCount = computed(
    () => this.seats().filter((seat) => seat.state !== 'empty').length,
  );
}

function iconFor(state: SeatState): string {
  switch (state) {
    case 'secured':
      return '🔒';
    case 'occupied-unsecured':
      return '⚠';
    default:
      return '·';
  }
}

function labelFor(state: SeatState): string {
  switch (state) {
    case 'secured':
      return 'secured';
    case 'occupied-unsecured':
      return 'occupied, not secured';
    default:
      return 'empty';
  }
}
