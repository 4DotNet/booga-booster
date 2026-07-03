import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { Hub, Mill } from '../../models/ride.models';

/**
 * Middle-right panel: the main mill rotation speed and every hub's individual
 * speed, in rpm. It is a polite live region so updates are announced without
 * interrupting the operator, and every value carries its unit.
 */
@Component({
  selector: 'bb-speed-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="speeds" aria-labelledby="speed-heading" aria-live="polite">
      <h2 id="speed-heading">Rotation speed</h2>
      <p class="mill">
        <span>Central mill</span>
        <span class="value" [attr.aria-label]="mill().speedRpm + ' revolutions per minute'">
          {{ mill().speedRpm }} rpm
        </span>
      </p>
      <ul class="hubs">
        @for (hub of hubs(); track hub.id) {
          <li class="hub">
            <span>Hub {{ hub.id }}</span>
            <span class="value" [attr.aria-label]="hub.speedRpm + ' revolutions per minute'">
              {{ hub.speedRpm }} rpm
            </span>
          </li>
        }
      </ul>
    </section>
  `,
  styles: `
    .speeds {
      display: grid;
      gap: 0.6rem;
    }
    h2 {
      margin: 0;
      font-size: 1rem;
    }
    .mill,
    .hub {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      margin: 0;
    }
    .mill {
      font-weight: 600;
      padding-bottom: 0.4rem;
      border-bottom: 1px solid var(--bb-border);
    }
    .hubs {
      list-style: none;
      margin: 0;
      padding: 0;
      display: grid;
      gap: 0.25rem;
    }
    .value {
      font-variant-numeric: tabular-nums;
    }
  `,
})
export class SpeedPanel {
  readonly mill = input.required<Mill>();
  readonly hubs = input.required<readonly Hub[]>();
}
