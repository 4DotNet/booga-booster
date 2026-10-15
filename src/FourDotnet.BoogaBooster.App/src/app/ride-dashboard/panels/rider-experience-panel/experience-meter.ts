import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { ProgressBar } from 'primeng/progressbar';

/** Design-token overrides that fit the bar to the dark dashboard palette. */
const BAR_TOKENS = {
  height: '0.6rem',
  background: 'var(--bb-border)',
  value: { background: 'var(--bb-accent)' },
};

/**
 * One labelled 0–100 meter. A `null` value is an explicit "nobody" state shown
 * as text (`emptyText`) instead of a 0 % bar, so "absent" is never mistaken
 * for "very low". The bar's accessible name carries its label and value with
 * a percent unit; the visible value text is hidden from assistive technology
 * to avoid reading it twice.
 */
@Component({
  selector: 'bb-experience-meter',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ProgressBar],
  host: { class: 'meter' },
  template: `
    <span class="label">{{ label() }}</span>
    @if (rounded(); as value) {
      <p-progressbar
        [value]="value.number"
        [showValue]="false"
        [dt]="barTokens"
        [attr.aria-label]="label() + ' ' + value.number + '%'"
      />
      <span class="value" aria-hidden="true">{{ value.number }}%</span>
    } @else {
      <span class="empty">{{ emptyText() }}</span>
    }
  `,
  styles: `
    :host {
      display: grid;
      grid-template-columns: 1fr auto;
      align-items: center;
      gap: 0.2rem 0.5rem;
    }
    .label {
      grid-column: 1 / -1;
      font-size: 0.8rem;
      color: var(--bb-muted);
    }
    p-progressbar {
      min-width: 0;
    }
    .value {
      min-width: 2.5rem;
      text-align: right;
      font-variant-numeric: tabular-nums;
    }
    .empty {
      grid-column: 1 / -1;
      font-style: italic;
    }
  `,
})
export class ExperienceMeter {
  readonly label = input.required<string>();
  /** Value on a 0–100 scale, or `null` when there is nobody to measure. */
  readonly value = input.required<number | null>();
  /** Text shown instead of a bar when {@link value} is `null`. */
  readonly emptyText = input.required<string>();

  protected readonly barTokens = BAR_TOKENS;

  /** Rounded and clamped value, boxed so a legitimate 0 stays truthy. */
  protected readonly rounded = computed(() => {
    const value = this.value();
    return value === null ? null : { number: Math.min(100, Math.max(0, Math.round(value))) };
  });
}
