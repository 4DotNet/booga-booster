import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MeterGroup, MeterItem } from 'primeng/metergroup';

/** A single rendered metric row: its label, meter fill and displayed/accessible text. */
interface MetricView {
  readonly key: string;
  readonly label: string;
  /**
   * The `MeterGroup` value array: one segment whose fill is the metric's
   * whole-number percentage, `0` for an empty/unavailable metric (the bar
   * then reads empty — the text next to it, not the meter, carries the
   * state).
   */
  readonly meterValue: MeterItem[];
  /** Visible value text: a whole-number percentage (`'72 %'`) or a state word (`'No riders'`). */
  readonly valueText: string;
  /** Accessible name for the meter: the metric's label plus its {@link valueText}. */
  readonly accessibleName: string;
  /** Whether {@link valueText} is a real value (for styling only — never the sole carrier of state). */
  readonly hasValue: boolean;
}

/**
 * Right-rail panel: the average happiness of the people waiting in the
 * queue, and the average happiness/nausea of the people currently riding.
 * Each metric is a labelled PrimeNG `MeterGroup` meter — one segment, sized
 * to the metric's percentage — with the whole-number percentage written out
 * as text next to it, and an explicit "Queue empty" / "No riders" /
 * "Unavailable" word standing in for the meter when there is no value to
 * show, so colour never carries the state alone. `MeterGroup`'s own default
 * label list is suppressed (an empty `#label` template) since this panel
 * renders its own label/value text above each meter.
 */
@Component({
  selector: 'bb-rider-mood-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MeterGroup],
  template: `
    <section class="rider-mood" aria-labelledby="rider-mood-heading">
      <h2 id="rider-mood-heading">Rider mood</h2>
      <ul class="metrics">
        @for (metric of metrics(); track metric.key) {
          <li class="metric">
            <div class="metric-header">
              <span class="metric-label">{{ metric.label }}</span>
              <span class="metric-value" [attr.data-state]="metric.hasValue ? 'value' : 'empty'">
                {{ metric.valueText }}
              </span>
            </div>
            <p-metergroup [value]="metric.meterValue" [attr.aria-label]="metric.accessibleName">
              <ng-template #label></ng-template>
            </p-metergroup>
          </li>
        }
      </ul>
    </section>
  `,
  styles: `
    :host {
      /* Scoped overrides so the meter fits the dashboard's dark palette. */
      --p-metergroup-meters-size: 0.5rem;
      --p-metergroup-meters-background: var(--bb-surface-2);
      --p-metergroup-border-radius: 0.35rem;
    }
    .rider-mood {
      display: grid;
      gap: 0.75rem;
    }
    h2 {
      margin: 0;
      font-size: 1rem;
    }
    .metrics {
      list-style: none;
      margin: 0;
      padding: 0;
      display: grid;
      gap: 0.6rem;
    }
    .metric {
      display: grid;
      gap: 0.3rem;
    }
    .metric-header {
      display: flex;
      justify-content: space-between;
      align-items: baseline;
      gap: 0.75rem;
    }
    .metric-label {
      color: var(--bb-muted);
    }
    .metric-value {
      font-weight: 600;
      font-variant-numeric: tabular-nums;
    }
    .metric-value[data-state='empty'] {
      color: var(--bb-muted);
      font-weight: 400;
      font-style: italic;
    }
  `,
})
export class RiderMoodPanel {
  /** Average happiness, in `[0, 1]`, of the people waiting in the queue; `null` when the queue is empty. */
  readonly queueHappiness = input.required<number | null>();
  /** Average happiness, in `[0, 1]`, of the people currently riding; `null` when nobody is aboard. */
  readonly riderHappiness = input.required<number | null>();
  /** Average nausea, in `[0, 1]`, of the people currently riding; `null` when nobody is aboard. */
  readonly nausea = input.required<number | null>();
  /** Whether the queue feed is unavailable (e.g. the poll failed). */
  readonly queueUnavailable = input.required<boolean>();

  private readonly queueMetric = computed<MetricView>(() =>
    this.queueUnavailable()
      ? emptyMetric('Queue happiness', 'queue-happiness', 'Unavailable')
      : metricView('Queue happiness', 'queue-happiness', this.queueHappiness(), 'Queue empty'),
  );

  private readonly riderMetric = computed<MetricView>(() =>
    metricView('Rider happiness', 'rider-happiness', this.riderHappiness(), 'No riders'),
  );

  private readonly nauseaMetric = computed<MetricView>(() =>
    metricView('Nausea', 'nausea', this.nausea(), 'No riders'),
  );

  protected readonly metrics = computed<readonly MetricView[]>(() => [
    this.queueMetric(),
    this.riderMetric(),
    this.nauseaMetric(),
  ]);
}

/** Builds a metric with a real value, or falls back to `emptyText` when `value` is `null`. */
function metricView(
  label: string,
  key: string,
  value: number | null,
  emptyText: string,
): MetricView {
  if (value === null) {
    return emptyMetric(label, key, emptyText);
  }
  const percent = Math.round(clamp01(value) * 100);
  const valueText = `${percent} %`;
  return {
    key,
    label,
    meterValue: [{ value: percent, color: 'var(--bb-accent)' }],
    valueText,
    accessibleName: `${label}: ${valueText}`,
    hasValue: true,
  };
}

/** Builds a metric with no value to show, carrying `text` as both the display and accessible state. */
function emptyMetric(label: string, key: string, text: string): MetricView {
  return {
    key,
    label,
    meterValue: [{ value: 0, color: 'var(--bb-accent)' }],
    valueText: text,
    accessibleName: `${label}: ${text}`,
    hasValue: false,
  };
}

/** Clamp a value into the inclusive `[0, 1]` range. */
function clamp01(value: number): number {
  return Math.min(1, Math.max(0, value));
}
