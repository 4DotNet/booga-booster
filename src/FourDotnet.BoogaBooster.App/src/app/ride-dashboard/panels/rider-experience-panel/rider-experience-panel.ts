import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { ProgressBar, ProgressBarPassThrough } from 'primeng/progressbar';

import { NO_RIDER_EXPERIENCE, RiderExperience } from '../../models/ride.models';

/** Text shown, instead of a value, when a metric has no data. */
const NO_DATA = 'No data';

interface MetricRow {
  readonly id: string;
  readonly label: string;
  /** Whole-number value on the 0-100 scale; `null` when there is no data. */
  readonly value: number | null;
  /** Visible (and spoken) value text: the rounded number, or "No data". */
  readonly text: string;
  /**
   * PrimeNG passthrough that corrects the progressbar's host attributes:
   * drops `aria-level` (not valid on the progressbar role, and PrimeNG fills
   * it with a value such as "72%") and removes `aria-valuenow` when there is
   * no data so a bar is never read as 0.
   */
  readonly pt: ProgressBarPassThrough;
}

/** Rounds to a whole number so the live region does not chatter at 30 Hz. */
function toRow(id: string, label: string, raw: number | null): MetricRow {
  const value = raw === null ? null : Math.round(raw);
  return {
    id,
    label,
    value,
    text: value === null ? NO_DATA : String(value),
    pt: { host: { 'aria-level': null, 'aria-valuenow': value } },
  };
}

/**
 * Rider experience panel: four labelled 0-100 progress bars for the queue's
 * average happiness and the riders' average happiness, preferred intensity and
 * nausea. Purely presentational. Every value is also shown as text next to its
 * bar, a missing value reads "No data" rather than 0, and the rows sit in a
 * polite live region so updates are announced.
 */
@Component({
  selector: 'bb-rider-experience-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ProgressBar],
  template: `
    <section class="experience" aria-labelledby="rider-experience-heading">
      <h2 id="rider-experience-heading">Rider experience</h2>
      <ul class="rows" aria-live="polite">
        @for (row of rows(); track row.id) {
          <li class="row" [attr.data-metric]="row.id">
            <span class="label" [id]="'rider-experience-' + row.id">{{ row.label }}</span>
            <p-progressbar
              [value]="row.value ?? 0"
              [showValue]="false"
              [attr.aria-labelledby]="'rider-experience-' + row.id"
              [attr.aria-valuetext]="row.text"
              [pt]="row.pt"
            />
            <span class="value" [class.no-data]="row.value === null">{{ row.text }}</span>
          </li>
        }
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
    .rows {
      list-style: none;
      margin: 0;
      padding: 0;
      display: grid;
      gap: 0.5rem;
    }
    .row {
      display: grid;
      grid-template-columns: 7rem minmax(0, 1fr) 4rem;
      align-items: center;
      gap: 0.5rem;
      font-size: 0.85rem;
    }
    .value {
      text-align: end;
      font-variant-numeric: tabular-nums;
      font-weight: 700;
    }
    .value.no-data {
      font-weight: 400;
      color: var(--bb-muted);
    }
    p-progressbar {
      --p-progressbar-background: var(--bb-surface-2);
      --p-progressbar-value-background: var(--bb-accent);
      --p-progressbar-height: 0.6rem;
    }
  `,
})
export class RiderExperiencePanel {
  /** Average happiness (0-100) of everyone queued; `null` when the queue is empty. */
  readonly queueHappiness = input<number | null>(null);

  /** Average rider experience on the ride; all-null when nobody is aboard. */
  readonly riderExperience = input<RiderExperience>(NO_RIDER_EXPERIENCE);

  protected readonly rows = computed<readonly MetricRow[]>(() => {
    const rider = this.riderExperience();
    return [
      toRow('queue-happiness', 'Queue happiness', this.queueHappiness()),
      toRow('rider-happiness', 'Rider happiness', rider.averageHappiness),
      toRow('rider-intensity', 'Rider intensity', rider.averagePreferredIntensity),
      toRow('rider-nausea', 'Rider nausea', rider.averageNausea),
    ];
  });
}
