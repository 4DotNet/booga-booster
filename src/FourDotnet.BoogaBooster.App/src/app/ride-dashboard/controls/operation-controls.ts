import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';

import { MotorDirection } from '../models/ride.models';
import { RideLifecycleService } from '../state/ride-lifecycle.service';
import { RideStateService } from '../state/ride-state.service';

/**
 * Left-rail operator controls. Power sliders and direction toggles drive the
 * central mill and hub motors through the ride-state service and stay in sync
 * with the service's current values.
 *
 * Every interactive control here only takes effect on the server while the
 * ride is actually running, so all of them — including the "Apply brakes"
 * action — are disabled unless the lifecycle state is `'started'`.
 */
@Component({
  selector: 'bb-operation-controls',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule],
  template: `
    <section
      class="controls"
      aria-labelledby="controls-heading"
      [attr.aria-describedby]="isStarted() ? null : 'controls-hint'"
    >
      <h2 id="controls-heading">Operation controls</h2>

      @if (!isStarted()) {
        <p id="controls-hint" class="hint">Controls become available once the ride is started.</p>
      }

      <form [formGroup]="form">
        <div class="control">
          <div class="control-head">
            <label for="mill-power">Central mill power</label>
            <output for="mill-power">{{ millPower() }}%</output>
          </div>
          <div class="control-row">
            <input
              id="mill-power"
              type="range"
              min="0"
              max="100"
              step="1"
              formControlName="millPower"
              aria-label="Central mill power, percent"
            />
            <span id="mill-dir-label" class="sr-only">Central mill direction</span>
            <button
              type="button"
              class="toggle"
              [disabled]="!isStarted()"
              [attr.aria-pressed]="millReverse()"
              aria-labelledby="mill-dir-label"
              (click)="toggleMillDirection()"
            >
              {{ directionLabel(millDirection()) }}
            </button>
          </div>
        </div>

        <div class="control">
          <div class="control-head">
            <label for="hub-power">Hub power</label>
            <output for="hub-power">{{ hubPower() }}%</output>
          </div>
          <div class="control-row">
            <input
              id="hub-power"
              type="range"
              min="0"
              max="100"
              step="1"
              formControlName="hubPower"
              aria-label="Hub motor power, percent"
            />
            <span id="hub-dir-label" class="sr-only">Hub direction</span>
            <button
              type="button"
              class="toggle"
              [disabled]="!isStarted()"
              [attr.aria-pressed]="hubReverse()"
              aria-labelledby="hub-dir-label"
              (click)="toggleHubDirection()"
            >
              {{ directionLabel(hubDirection()) }}
            </button>
          </div>
        </div>
      </form>

      <div class="control">
        <div class="control-row">
          <span id="brake-label">Gondola brakes</span>
          <button
            type="button"
            class="toggle brake"
            [disabled]="!isStarted()"
            [attr.aria-pressed]="gondolaBrakeEngaged()"
            aria-labelledby="brake-label"
            (click)="toggleGondolaBrake()"
          >
            {{ brakeLabel() }}
          </button>
        </div>
      </div>

      <div class="control">
        <div class="control-row">
          <button
            type="button"
            class="apply-brakes"
            [disabled]="!isStarted()"
            [attr.aria-pressed]="brakesEngaged()"
            (click)="toggleEngineBrakes()"
          >
            {{ engineBrakeLabel() }}
          </button>
        </div>
      </div>
    </section>
  `,
  styles: `
    .controls {
      display: grid;
      gap: 1rem;
    }
    h2 {
      margin: 0;
      font-size: 1rem;
    }
    form {
      display: grid;
      gap: 1rem;
    }
    .control {
      display: grid;
      gap: 0.4rem;
    }
    .control-head {
      display: flex;
      justify-content: space-between;
      align-items: baseline;
    }
    .control-row {
      display: flex;
      align-items: center;
      gap: 0.75rem;
    }
    #brake-label {
      flex: 1 1 auto;
    }
    output {
      font-weight: 600;
      font-variant-numeric: tabular-nums;
    }
    input[type='range'] {
      flex: 1 1 auto;
      min-width: 0;
      accent-color: var(--bb-accent);
    }
    .sr-only {
      position: absolute;
      width: 1px;
      height: 1px;
      padding: 0;
      margin: -1px;
      overflow: hidden;
      clip: rect(0, 0, 0, 0);
      white-space: nowrap;
      border: 0;
    }
    .toggle {
      flex: 0 0 auto;
      min-width: 5.5rem;
      text-align: center;
      padding: 0.4rem 0.9rem;
      border-radius: 0.5rem;
      border: 1px solid var(--bb-border);
      background: var(--bb-surface-2);
      color: inherit;
      font: inherit;
      cursor: pointer;
    }
    .toggle[aria-pressed='true'] {
      background: var(--bb-accent);
      border-color: var(--bb-accent);
      color: var(--bb-accent-contrast);
    }
    .toggle:focus-visible {
      outline: 2px solid var(--bb-focus);
      outline-offset: 2px;
    }
    .toggle.brake {
      min-width: 9.5rem;
    }
    .apply-brakes {
      flex: 1 1 auto;
      padding: 0.5rem 0.9rem;
      border-radius: 0.5rem;
      border: 1px solid var(--bb-alert);
      background: transparent;
      color: var(--bb-alert);
      font: inherit;
      font-weight: 600;
      cursor: pointer;
    }
    .apply-brakes:focus-visible {
      outline: 2px solid var(--bb-focus);
      outline-offset: 2px;
    }
    .apply-brakes[aria-pressed='true'] {
      background: var(--bb-alert);
      color: var(--bb-accent-contrast);
    }
    .toggle:disabled,
    .apply-brakes:disabled,
    input[type='range']:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }
    .hint {
      margin: 0;
      font-size: 0.85rem;
      color: var(--bb-muted);
    }
  `,
})
export class OperationControls {
  private readonly rideState = inject(RideStateService);
  private readonly lifecycle = inject(RideLifecycleService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(FormBuilder);

  /** Operator controls only take effect on the server while the ride runs. */
  protected readonly isStarted = computed(() => this.lifecycle.state() === 'started');

  protected readonly millPower = computed(() => this.rideState.mill().power);
  protected readonly hubPower = this.rideState.hubPower;
  protected readonly millDirection = computed(() => this.rideState.mill().direction);
  protected readonly hubDirection = this.rideState.hubDirection;
  protected readonly gondolaBrakeEngaged = this.rideState.gondolaBrakeEngaged;
  protected readonly brakesEngaged = this.rideState.brakesEngaged;

  protected readonly form = this.fb.nonNullable.group({
    millPower: this.rideState.mill().power,
    hubPower: this.rideState.hubPower(),
  });

  constructor() {
    this.form.controls.millPower.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((value) => this.rideState.setMillPower(Number(value)));

    this.form.controls.hubPower.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((value) => this.rideState.setHubPower(Number(value)));

    // Keep the sliders in sync when power changes elsewhere, without looping.
    effect(() => {
      const power = this.rideState.mill().power;
      if (this.form.controls.millPower.value !== power) {
        this.form.controls.millPower.setValue(power, { emitEvent: false });
      }
    });
    effect(() => {
      const power = this.rideState.hubPower();
      if (this.form.controls.hubPower.value !== power) {
        this.form.controls.hubPower.setValue(power, { emitEvent: false });
      }
    });

    // Reactive forms manage their own `disabled` DOM state: toggling it via
    // `enable()`/`disable()` (rather than a `[disabled]` binding on the
    // `formControlName` inputs) is what actually reaches the native element
    // and avoids Angular's "disabled attribute with a reactive form
    // directive" warning. The sliders stay disabled while the engine brake
    // is engaged, even though the "Apply brakes" toggle itself stays enabled
    // so the operator can release it again.
    effect(() => {
      if (this.isStarted() && !this.brakesEngaged()) {
        this.form.enable({ emitEvent: false });
      } else {
        this.form.disable({ emitEvent: false });
      }
    });
  }

  protected millReverse(): boolean {
    return this.rideState.mill().direction === 'reverse';
  }

  protected hubReverse(): boolean {
    return this.rideState.hubDirection() === 'reverse';
  }

  protected directionLabel(direction: MotorDirection): string {
    return direction === 'reverse' ? 'Reverse' : 'Forward';
  }

  protected toggleMillDirection(): void {
    this.rideState.setMillDirection(this.millReverse() ? 'forward' : 'reverse');
  }

  protected toggleHubDirection(): void {
    this.rideState.setHubDirection(this.hubReverse() ? 'forward' : 'reverse');
  }

  protected brakeLabel(): string {
    return this.gondolaBrakeEngaged() ? 'Gondolas Break' : 'Gondolas Released';
  }

  protected toggleGondolaBrake(): void {
    this.rideState.setGondolaBrake(!this.gondolaBrakeEngaged());
  }

  /** Visible label mirroring the engine brake's reported pressed state. */
  protected engineBrakeLabel(): string {
    return this.brakesEngaged() ? 'Release brakes' : 'Apply brakes';
  }

  /** Toggle the engine brake to the opposite of its currently reported state. */
  protected toggleEngineBrakes(): void {
    this.rideState.setEngineBrakes(!this.brakesEngaged());
  }
}
