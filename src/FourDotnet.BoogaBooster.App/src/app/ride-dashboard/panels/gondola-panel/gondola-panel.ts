import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { Gondola } from '../../models/ride.models';

interface GondolaView {
  readonly id: number;
  readonly occupied: number;
  readonly vertical: string;
  readonly lateral: string;
  readonly verticalLabel: string;
  readonly lateralLabel: string;
}

/**
 * Bottom-right panel: all 16 gondolas, each showing its occupied-seat count and
 * signed vertical and lateral g-forces. It is a polite live region and every
 * numeric value carries its unit in the accessible name.
 */
@Component({
  selector: 'bb-gondola-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="gondolas" aria-labelledby="gondola-heading" aria-live="polite">
      <h2 id="gondola-heading">Gondolas</h2>
      <ul class="list">
        @for (gondola of rows(); track gondola.id) {
          <li class="row">
            <span class="name">Gondola {{ gondola.id }}</span>
            <span class="occupied">{{ gondola.occupied }} seated</span>
            <span class="g" [attr.aria-label]="gondola.verticalLabel">
              V {{ gondola.vertical }} g
            </span>
            <span class="g" [attr.aria-label]="gondola.lateralLabel">
              L {{ gondola.lateral }} g
            </span>
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
    .list {
      list-style: none;
      margin: 0;
      padding: 0;
      display: grid;
      gap: 0.2rem;
    }
    .row {
      display: grid;
      grid-template-columns: 1fr auto auto auto;
      gap: 0.6rem;
      align-items: baseline;
      padding: 0.2rem 0;
      border-bottom: 1px solid var(--bb-border);
    }
    .name {
      font-weight: 600;
    }
    .occupied,
    .g {
      color: var(--bb-muted);
      font-variant-numeric: tabular-nums;
    }
    .g {
      text-align: right;
    }
  `,
})
export class GondolaPanel {
  readonly gondolas = input.required<readonly Gondola[]>();

  protected readonly rows = computed<GondolaView[]>(() =>
    this.gondolas().map((gondola) => {
      const occupied = gondola.seats.filter((seat) => seat.state !== 'empty').length;
      const vertical = signed(gondola.gForce.vertical);
      const lateral = signed(gondola.gForce.lateral);
      return {
        id: gondola.id,
        occupied,
        vertical,
        lateral,
        verticalLabel: `Gondola ${gondola.id} vertical g-force ${vertical} g`,
        lateralLabel: `Gondola ${gondola.id} lateral g-force ${lateral} g`,
      };
    }),
  );
}

/** Format a g-force with an explicit sign so direction is unambiguous. */
function signed(value: number): string {
  const fixed = value.toFixed(1);
  return value > 0 ? `+${fixed}` : fixed;
}
