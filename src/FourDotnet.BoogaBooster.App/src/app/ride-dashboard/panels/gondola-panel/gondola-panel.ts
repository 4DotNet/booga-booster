import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { Gondola, SeatState } from '../../models/ride.models';

/**
 * Severity of a gondola metric against its safe limits: `normal` within
 * limits, `warn` for an elevated-but-tolerable value, `alert` for a value
 * over the safe limit.
 */
type MetricLevel = 'normal' | 'warn' | 'alert';

/** Combined gondola weight (kg) above which the value is elevated (warn). */
const WEIGHT_WARN_KG = 250;

/** Combined gondola weight (kg) above which the value is over the safe limit (alert). */
const WEIGHT_ALERT_KG = 300;

/** Vertical g-force magnitude above which the value is over the safe limit (alert). */
const VERTICAL_G_LIMIT = 4.5;

/** Lateral g-force magnitude above which the value is over the safe limit (alert). */
const LATERAL_G_LIMIT = 2;

interface SeatView {
  readonly key: string;
  readonly state: SeatState;
  readonly icon: string;
  readonly label: string;
}

interface GondolaView {
  readonly id: number;
  readonly seats: readonly SeatView[];
  readonly weightKg: number;
  readonly weightLevel: MetricLevel;
  readonly weightLabel: string;
  readonly vertical: string;
  readonly verticalLevel: MetricLevel;
  readonly lateral: string;
  readonly lateralLevel: MetricLevel;
  readonly verticalLabel: string;
  readonly lateralLabel: string;
}

/**
 * Bottom-right panel: a compact visual card per gondola. Each card draws its
 * two seats as small squares — grey when empty, green when occupied and
 * secured, red when occupied but not secured — with the gondola's combined
 * passenger weight and signed g-forces underneath in a small font. Colour
 * never carries state alone: every seat also gets an icon and an sr-only
 * accessible name, and every metric's accessible name spells out its unit.
 */
@Component({
  selector: 'bb-gondola-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="gondolas" aria-labelledby="gondola-heading" aria-live="polite">
      <h2 id="gondola-heading">Gondolas</h2>
      <ul class="grid">
        @for (gondola of rows(); track gondola.id) {
          <li class="card">
            <span class="sr-only">Gondola {{ gondola.id }}</span>
            <div class="seats">
              @for (seat of gondola.seats; track seat.key) {
                <span class="seat" [attr.data-state]="seat.state">
                  <span class="icon" aria-hidden="true">{{ seat.icon }}</span>
                  <span class="sr-only">{{ seat.label }}</span>
                </span>
              }
            </div>
            <div class="metrics">
              <span
                class="metric"
                [attr.data-level]="gondola.weightLevel"
                [attr.aria-label]="gondola.weightLabel"
              >
                {{ gondola.weightKg }} kg
              </span>
              <span
                class="metric"
                [attr.data-level]="gondola.verticalLevel"
                [attr.aria-label]="gondola.verticalLabel"
              >
                V {{ gondola.vertical }} g
              </span>
              <span
                class="metric"
                [attr.data-level]="gondola.lateralLevel"
                [attr.aria-label]="gondola.lateralLabel"
              >
                L {{ gondola.lateral }} g
              </span>
            </div>
          </li>
        }
      </ul>
    </section>
  `,
  styles: `
    .gondolas {
      display: grid;
      gap: 0.5rem;
    }
    h2 {
      margin: 0;
      font-size: 1rem;
    }
    .grid {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-wrap: wrap;
      gap: 0.35rem;
    }
    .card {
      /* Exactly 4 cards per row (16 gondolas -> a 4x4 grid): each card's
         hypothetical main size is a quarter of the row minus its share of
         the three inter-card gaps, so four of them fill one line exactly. */
      flex: 0 1 calc(25% - 0.2625rem);
      box-sizing: border-box;
      min-width: 0;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.2rem;
      padding: 0.25rem;
      border: 1px solid var(--bb-border);
      border-radius: 0.35rem;
      background: var(--bb-surface-2);
    }
    .seats {
      display: flex;
      gap: 0.15rem;
    }
    .seat {
      display: grid;
      place-items: center;
      width: 0.8rem;
      height: 0.8rem;
      border-radius: 0.2rem;
      border: 1px solid var(--bb-border);
      background: var(--bb-muted);
    }
    .seat[data-state='secured'] {
      background: var(--bb-ok);
    }
    .seat[data-state='occupied-unsecured'] {
      background: var(--bb-alert);
    }
    .icon {
      font-size: 0.45rem;
      line-height: 1;
    }
    .metrics {
      display: flex;
      flex-direction: column;
      align-items: center;
      font-size: 0.6rem;
      line-height: 1.25;
      color: var(--bb-muted);
      font-variant-numeric: tabular-nums;
    }
    /* An out-of-limit metric becomes a filled badge: orange when elevated,
       red when over the safe limit. Dark text on the fill keeps the value
       legible (WCAG AA), and the accessible name spells out the status so
       colour never carries the meaning alone. */
    .metric[data-level='warn'],
    .metric[data-level='alert'] {
      color: var(--bb-accent-contrast);
      font-weight: 700;
      border-radius: 0.25rem;
      padding: 0 0.3rem;
    }
    .metric[data-level='warn'] {
      background: var(--bb-alert);
    }
    .metric[data-level='alert'] {
      background: var(--bb-danger);
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
export class GondolaPanel {
  readonly gondolas = input.required<readonly Gondola[]>();

  protected readonly rows = computed<GondolaView[]>(() =>
    this.gondolas().map((gondola) => {
      const seats = gondola.seats.map((seat, index) => ({
        key: `${gondola.id}-${seat.id}`,
        state: seat.state,
        icon: iconFor(seat.state),
        label: `Gondola ${gondola.id} ${positionFor(index)} seat: ${labelFor(seat.state)}`,
      }));
      const weightKg = gondola.seats.reduce((total, seat) => total + seat.occupiedKg, 0);
      const vertical = signed(gondola.gForce.vertical);
      const lateral = signed(gondola.gForce.lateral);
      const weightLevel = weightLevelFor(weightKg);
      const verticalLevel = gForceLevelFor(gondola.gForce.vertical, VERTICAL_G_LIMIT);
      const lateralLevel = gForceLevelFor(gondola.gForce.lateral, LATERAL_G_LIMIT);
      return {
        id: gondola.id,
        seats,
        weightKg,
        weightLevel,
        weightLabel: `Gondola ${gondola.id} weight ${weightKg} kg${statusSuffix(weightLevel)}`,
        vertical,
        verticalLevel,
        lateral,
        lateralLevel,
        verticalLabel: `Gondola ${gondola.id} vertical g-force ${vertical} g${statusSuffix(verticalLevel)}`,
        lateralLabel: `Gondola ${gondola.id} lateral g-force ${lateral} g${statusSuffix(lateralLevel)}`,
      };
    }),
  );
}

/** Classifies a gondola's combined weight against its warn/alert limits. */
function weightLevelFor(weightKg: number): MetricLevel {
  if (weightKg > WEIGHT_ALERT_KG) {
    return 'alert';
  }
  if (weightKg > WEIGHT_WARN_KG) {
    return 'warn';
  }
  return 'normal';
}

/**
 * Classifies a signed g-force against its safe magnitude limit: `alert` once
 * the value passes the limit in either direction (e.g. above +4.5 or below
 * -4.5), otherwise `normal`.
 */
function gForceLevelFor(value: number, limit: number): MetricLevel {
  return Math.abs(value) > limit ? 'alert' : 'normal';
}

/** Accessible-name suffix that spells out a non-normal metric's status. */
function statusSuffix(level: MetricLevel): string {
  switch (level) {
    case 'alert':
      return ', over safe limit';
    case 'warn':
      return ', above warning limit';
    default:
      return '';
  }
}

/** Left/right label for a gondola's two seats, by their position index. */
function positionFor(index: number): string {
  return index === 0 ? 'left' : 'right';
}

/** Format a g-force with an explicit sign so direction is unambiguous. */
function signed(value: number): string {
  const fixed = value.toFixed(1);
  return value > 0 ? `+${fixed}` : fixed;
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
      return 'occupied, secured';
    case 'occupied-unsecured':
      return 'occupied, not secured';
    default:
      return 'empty';
  }
}
