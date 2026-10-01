import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import {
  MAX_G_FORCE,
  averageHappiness,
  averageNausea,
  averagePreferredG,
  countMad,
  countSick,
} from '../../../mood/mood.models';
import { QueueGuest } from '../../../queue/models/queue.models';
import { LastOffload, SeatRider } from '../../models/ride.models';
import { MoodBar } from './mood-bar';

/** Text shown instead of a value when the population is empty. */
export const NO_VALUE = '—';

/** Whole-number text for a 0–100 score, or {@link NO_VALUE} when there is no data. */
function score(value: number | null): string {
  return value === null ? NO_VALUE : String(Math.round(value));
}

/**
 * Rider Experience panel: four labelled mood bars (queue happiness and the
 * seated riders' happiness, preferred G and nausea) plus the mad / sick counts
 * as plain text — the accessible equivalent of the 3D scene's head colours.
 * It deliberately has no live region: the bars update at telemetry rate and
 * must not add to what assistive technology announces.
 */
@Component({
  selector: 'bb-rider-experience-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MoodBar],
  template: `
    <section class="experience" aria-labelledby="rider-experience-heading">
      <h2 id="rider-experience-heading">Rider experience</h2>
      <bb-mood-bar
        label="Queue happiness"
        [percent]="queueHappiness()"
        [valueText]="queueHappinessText()"
      />
      <bb-mood-bar
        label="Rider happiness"
        [percent]="riderHappiness()"
        [valueText]="riderHappinessText()"
      />
      <bb-mood-bar
        label="Rider preferred G"
        [percent]="preferredGPercent()"
        [valueText]="preferredGText()"
      />
      <bb-mood-bar label="Rider nausea" [percent]="riderNausea()" [valueText]="riderNauseaText()" />
      <ul class="counts">
        <li>{{ madInQueue() }} mad in queue</li>
        <li>{{ sickOnRide() }} sick on ride</li>
        <li>{{ leftSick() }} left sick</li>
      </ul>
    </section>
  `,
  styles: `
    .experience {
      display: grid;
      gap: 0.5rem;
    }
    h2 {
      margin: 0;
      font-size: 1rem;
    }
    .counts {
      list-style: none;
      margin: 0;
      padding: 0;
      display: grid;
      gap: 0.1rem;
      font-size: 0.8rem;
    }
  `,
})
export class RiderExperiencePanel {
  /** Everyone currently in the queue. */
  readonly queuedGuests = input.required<readonly QueueGuest[]>();
  /** Everyone currently seated on the ride. */
  readonly riders = input.required<readonly SeatRider[]>();
  /** The most recent offload snapshot. */
  readonly lastOffload = input<LastOffload | null>(null);

  protected readonly queueHappiness = computed(() => averageHappiness(this.queuedGuests()));
  protected readonly riderHappiness = computed(() => averageHappiness(this.riders()));
  protected readonly riderNausea = computed(() => averageNausea(this.riders()));
  private readonly preferredG = computed(() => averagePreferredG(this.riders()));

  protected readonly preferredGPercent = computed(() => {
    const g = this.preferredG();
    return g === null ? null : (g / MAX_G_FORCE) * 100;
  });

  protected readonly queueHappinessText = computed(() => score(this.queueHappiness()));
  protected readonly riderHappinessText = computed(() => score(this.riderHappiness()));
  protected readonly riderNauseaText = computed(() => score(this.riderNausea()));
  protected readonly preferredGText = computed(() => {
    const g = this.preferredG();
    return g === null ? NO_VALUE : `${g.toFixed(1)} g`;
  });

  protected readonly madInQueue = computed(() => countMad(this.queuedGuests()));
  protected readonly sickOnRide = computed(() => countSick(this.riders()));
  protected readonly leftSick = computed(() => countSick(this.lastOffload()?.riders ?? []));
}
