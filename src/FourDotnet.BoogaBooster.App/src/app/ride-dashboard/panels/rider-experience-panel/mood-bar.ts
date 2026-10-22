import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { ProgressBar } from 'primeng/progressbar';

/** Text shown, and announced, in place of a value when there is nobody to average. */
export const NO_GUESTS = 'No guests';

/**
 * One labelled 0-100 mood bar. A `null` value means there is nobody to
 * average: the bar then carries no value at all (it is not drawn at 0) and
 * both the visible text and the accessible value read "No guests". The value is
 * always also shown as text, so the bar length never carries the meaning alone.
 */
@Component({
  selector: 'bb-mood-bar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ProgressBar],
  template: `
    <div class="row">
      <span class="label" [id]="labelId()">{{ label() }}</span>
      <span class="value" aria-hidden="true">{{ text() }}</span>
    </div>
    <div
      role="progressbar"
      class="bar"
      aria-valuemin="0"
      aria-valuemax="100"
      [attr.aria-labelledby]="labelId()"
      [attr.aria-valuenow]="rounded()"
      [attr.aria-valuetext]="valueText()"
    >
      <p-progressbar aria-hidden="true" [value]="rounded() ?? undefined" [showValue]="false" />
    </div>
  `,
  styles: `
    :host {
      display: grid;
      gap: 0.25rem;
      --p-progressbar-height: 0.6rem;
      --p-progressbar-background: var(--bb-surface-2);
      --p-progressbar-value-background: var(--bb-accent);
    }
    .row {
      display: flex;
      justify-content: space-between;
      align-items: baseline;
      gap: 1rem;
    }
    .value {
      font-variant-numeric: tabular-nums;
      color: var(--bb-muted);
    }
  `,
})
export class MoodBar {
  /** Visible and accessible name of the bar. */
  readonly label = input.required<string>();

  /** Average on a 0-100 scale, or `null` when there is nobody to average. */
  readonly value = input.required<number | null>();

  protected readonly rounded = computed(() => {
    const value = this.value();
    return value === null ? null : Math.round(value);
  });

  protected readonly text = computed(() => this.rounded()?.toString() ?? NO_GUESTS);

  /** Spoken value: "12 out of 100", or "No guests" when there is no aria-valuenow. */
  protected readonly valueText = computed(() =>
    this.rounded() === null ? NO_GUESTS : `${this.rounded()} out of 100`,
  );

  protected readonly labelId = computed(
    () => `mood-${this.label().toLowerCase().replaceAll(/\s+/g, '-')}`,
  );
}
