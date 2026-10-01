import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  afterRenderEffect,
  computed,
  input,
  viewChild,
} from '@angular/core';
import { ProgressBar } from 'primeng/progressbar';

let nextId = 0;

/**
 * One labelled mood meter: a visible label, the value as text and a PrimeNG
 * progress bar named by that label. `percent` is the bar fill (0–100) and
 * `null` renders an empty bar; `valueText` is what is shown and spoken
 * (e.g. `70`, `3.3 g` or `—` when there is no data).
 */
@Component({
  selector: 'bb-mood-bar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ProgressBar],
  template: `
    <div class="row">
      <span class="label" [id]="labelId">{{ label() }}</span>
      <span class="value">{{ valueText() }}</span>
    </div>
    <p-progressbar
      #bar
      [value]="fill()"
      [showValue]="false"
      [attr.aria-labelledby]="labelId"
      [attr.aria-valuetext]="valueText()"
    />
  `,
  styles: `
    :host {
      display: grid;
      gap: 0.2rem;
    }
    .row {
      display: flex;
      justify-content: space-between;
      font-size: 0.8rem;
    }
    .value {
      font-variant-numeric: tabular-nums;
      font-weight: 700;
    }
    p-progressbar {
      --p-progressbar-height: 0.6rem;
      --p-progressbar-background: var(--bb-surface-2);
      --p-progressbar-value-background: var(--bb-accent);
      --p-progressbar-border-radius: 0.3rem;
    }
  `,
})
export class MoodBar {
  readonly label = input.required<string>();
  readonly percent = input.required<number | null>();
  readonly valueText = input.required<string>();

  protected readonly labelId = `bb-mood-bar-${nextId++}`;
  protected readonly fill = computed(() => Math.min(100, Math.max(0, this.percent() ?? 0)));

  private readonly bar = viewChild.required('bar', { read: ElementRef<HTMLElement> });

  constructor() {
    // PrimeNG 22 (rc) stamps aria-level="NN%" on the progressbar host, which is
    // neither allowed on role=progressbar nor a valid value (axe: aria-allowed-attr,
    // aria-valid-attr-value). Strip it after each render; the label, valuenow and
    // valuetext already carry the meaning.
    afterRenderEffect(() => {
      this.fill();
      this.bar().nativeElement.removeAttribute('aria-level');
    });
  }
}
