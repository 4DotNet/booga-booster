import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { ExperienceMeter } from './experience-meter';

/**
 * Right-rail panel summarising how guests feel: the average happiness of the
 * queue, and the average happiness, preferred intensity and nausea of the
 * riders on board. Presentational; all values are 0–100 or `null` for
 * "nobody" (empty queue / empty ride).
 */
@Component({
  selector: 'bb-rider-experience-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ExperienceMeter],
  template: `
    <section class="experience" aria-labelledby="rider-experience-heading" aria-live="polite">
      <h2 id="rider-experience-heading">Rider Experience</h2>
      <bb-experience-meter
        label="Queue happiness"
        emptyText="Queue empty"
        [value]="queueHappiness()"
      />
      <bb-experience-meter
        label="Rider happiness"
        emptyText="No riders"
        [value]="riderHappiness()"
      />
      <bb-experience-meter
        label="Rider intensity"
        emptyText="No riders"
        [value]="riderIntensity()"
      />
      <bb-experience-meter label="Rider nausea" emptyText="No riders" [value]="riderNausea()" />
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
