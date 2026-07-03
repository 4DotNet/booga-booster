import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { LoadBalanceState, SecurityState } from '../../models/ride.models';

/**
 * Top-right panel: an overview of passenger load and security, never per-seat
 * detail. Row one reports the occupied-seat count and whether every occupied
 * seat is secured; row two reports the total ride load weight and whether it
 * is balanced around the mill. Safe/unsafe is always conveyed by icon and
 * text, never colour alone.
 */
@Component({
  selector: 'bb-security-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="security" aria-labelledby="security-heading">
      <h2 id="security-heading">Load & security</h2>
      <p class="row" [attr.data-state]="loadState()">
        <span class="icon" aria-hidden="true">{{ loadIcon() }}</span>
        <span
          >Load: {{ passengers() }} passenger{{ passengers() === 1 ? '' : 's' }} —
          {{ loadState() }}</span
        >
      </p>
      <p class="row" [attr.data-state]="loadBalance()">
        <span class="icon" aria-hidden="true">{{ weightIcon() }}</span>
        <span>Weight: {{ totalKg() }} kilos — {{ loadBalance() }}</span>
      </p>
    </section>
  `,
  styles: `
    .security {
      display: grid;
      gap: 0.6rem;
    }
    h2 {
      margin: 0;
      font-size: 1rem;
    }
    .row {
      margin: 0;
      display: flex;
      align-items: center;
      gap: 0.4rem;
      font-weight: 600;
    }
    .row[data-state='safe'] {
      color: var(--bb-ok);
    }
    .row[data-state='unsafe'] {
      color: var(--bb-alert);
    }
    .icon {
      font-weight: 700;
      line-height: 1;
    }
  `,
})
export class SecurityPanel {
  readonly passengers = input.required<number>();
  readonly securityState = input.required<SecurityState>();
  readonly totalKg = input.required<number>();
  readonly loadBalance = input.required<LoadBalanceState>();

  protected readonly loadState = computed(() =>
    this.securityState() === 'secured' ? 'safe' : 'unsafe',
  );
  protected readonly loadIcon = computed(() => (this.loadState() === 'safe' ? '✓' : '⚠'));
  protected readonly weightIcon = computed(() => (this.loadBalance() === 'safe' ? '✓' : '⚠'));
}
