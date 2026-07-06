import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { Hub, Mill, MotorDirection } from '../../models/ride.models';

/**
 * Middle-right panel: the main mill rotation speed and every hub's individual
 * speed, in rpm. It is a polite live region so updates are announced without
 * interrupting the operator, and every value carries its unit.
 *
 * Telemetry speed is signed (negative in reverse); this panel shows the speed
 * magnitude with the rotation direction called out separately.
 */
@Component({
  selector: 'bb-speed-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="speeds" aria-labelledby="speed-heading" aria-live="polite">
      <h2 id="speed-heading">Rotation speed</h2>
      <p class="mill">
        <span>Central mill</span>
        <span class="value" [attr.aria-label]="speedLabel(mill().speedRpm, mill().direction)">
          {{ magnitude(mill().speedRpm) }} rpm
          <span class="dir">{{ directionWord(mill().direction) }}</span>
        </span>
      </p>
      <ul class="hubs">
        @for (hub of hubs(); track hub.id) {
          <li class="hub">
            <span>Hub {{ hub.id }}</span>
            <span class="value" [attr.aria-label]="speedLabel(hub.speedRpm, hub.direction)">
              {{ magnitude(hub.speedRpm) }} rpm
              <span class="dir">{{ directionWord(hub.direction) }}</span>
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
    .dir {
      margin-left: 0.4rem;
      font-size: 0.8em;
      text-transform: uppercase;
      letter-spacing: 0.03em;
      color: var(--bb-muted);
    }
  `,
})
export class SpeedPanel {
  readonly mill = input.required<Mill>();
  readonly hubs = input.required<readonly Hub[]>();

  /** The speed magnitude in rpm, rounded, without its direction sign. */
  protected magnitude(rpm: number): number {
    return Math.abs(rpm);
  }

  /** The rotation direction as a lowercase word. */
  protected directionWord(direction: MotorDirection): string {
    return direction === 'reverse' ? 'reverse' : 'forward';
  }

  /** Accessible label combining the speed magnitude and its direction. */
  protected speedLabel(rpm: number, direction: MotorDirection): string {
    return `${this.magnitude(rpm)} revolutions per minute, ${this.directionWord(direction)}`;
  }
}
