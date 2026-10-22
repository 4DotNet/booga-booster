import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { MoodBar } from './mood-bar';

/**
 * Bottom of the right rail: how the guests are feeling. Four 0-100 mood bars
 * — queue happiness, then the riders' happiness, preferred intensity and
 * nausea. An average of `null` (nobody queued / aboard) is shown as "No
 * guests" instead of an empty bar.
 */
@Component({
  selector: 'bb-rider-experience-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MoodBar],
  template: `
    <section class="experience" aria-labelledby="rider-experience-heading">
      <h2 id="rider-experience-heading">Rider Experience</h2>
      <bb-mood-bar label="Queue happiness" [value]="queueHappiness()" />
      <bb-mood-bar label="Rider happiness" [value]="riderHappiness()" />
      <bb-mood-bar label="Rider intensity" [value]="riderIntensity()" />
      <bb-mood-bar label="Rider nausea" [value]="riderNausea()" />
    </section>
  `,
  styles: `
    .experience {
      display: grid;
      gap: 0.6rem;
    }
    h2 {
      margin: 0;
      font-size: 1rem;
    }
  `,
})
export class RiderExperiencePanel {
  readonly queueHappiness = input.required<number | null>();
  readonly riderHappiness = input.required<number | null>();
  readonly riderIntensity = input.required<number | null>();
  readonly riderNausea = input.required<number | null>();
}
