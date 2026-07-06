import { Injectable, Signal, computed, inject } from '@angular/core';

import { RIDE_TELEMETRY_SOURCE } from '../data/ride-telemetry-source';
import {
  Gondola,
  Hub,
  LoadBalanceState,
  Mill,
  MotorDirection,
  RideState,
  SecurityState,
  clampPower,
  loadBalanceState,
  totalLoadKg,
} from '../models/ride.models';

/**
 * Single source of truth for ride state and operator commands.
 *
 * Read state is projected from the injected telemetry source; command methods
 * validate (clamping power to 0–100) and dispatch to the source, which today is
 * a simulator and later a real backend feed.
 */
@Injectable({ providedIn: 'root' })
export class RideStateService {
  private readonly source = inject(RIDE_TELEMETRY_SOURCE);

  private readonly telemetry = this.source.telemetry;

  /** Overall ride state (stopped/running/emergency). */
  readonly state: Signal<RideState> = computed(() => this.telemetry().state);

  /** The central mill: commanded power/direction and sensed speed. */
  readonly mill: Signal<Mill> = computed(() => this.telemetry().mill);

  /** Every hub with its individual sensed speed. */
  readonly hubs: Signal<readonly Hub[]> = computed(() => this.telemetry().hubs);

  /** Every gondola with its seats and g-forces. */
  readonly gondolas: Signal<readonly Gondola[]> = computed(() => this.telemetry().gondolas);

  /**
   * Total number of occupied seats across all gondolas. Prefers the
   * backend-streamed `boardedPassengerCount`; falls back to counting
   * non-empty seats when a frame doesn't carry that count (e.g. the at-rest
   * snapshot or an older frame shape).
   */
  readonly occupiedSeats: Signal<number> = computed(
    () =>
      this.telemetry().boardedPassengerCount ??
      this.gondolas().reduce(
        (total, gondola) => total + gondola.seats.filter((seat) => seat.state !== 'empty').length,
        0,
      ),
  );

  /** Secured only when every occupied seat is secured. */
  readonly securityState: Signal<SecurityState> = computed(() => {
    const anyUnsecured = this.gondolas().some((gondola) =>
      gondola.seats.some((seat) => seat.state === 'occupied-unsecured'),
    );
    return anyUnsecured ? 'unsecured' : 'secured';
  });

  /** Total ride load, in kg, across every seat. */
  readonly totalLoadKg: Signal<number> = computed(() => totalLoadKg(this.gondolas()));

  /** Safe only when the load's rotational eccentricity is within tolerance. */
  readonly loadBalanceState: Signal<LoadBalanceState> = computed(() =>
    loadBalanceState(this.gondolas()),
  );

  /** Commanded power of the hub motor group. */
  readonly hubPower: Signal<number> = computed(() => this.hubs()[0]?.power ?? 0);

  /** Commanded direction of the hub motor group. */
  readonly hubDirection: Signal<MotorDirection> = computed(
    () => this.hubs()[0]?.direction ?? 'forward',
  );

  /** Representative hub rotation speed (mean of the individual hubs), in rpm. */
  readonly hubSpeedRpm: Signal<number> = computed(() => {
    const hubs = this.hubs();
    if (hubs.length === 0) {
      return 0;
    }
    return hubs.reduce((total, hub) => total + hub.speedRpm, 0) / hubs.length;
  });

  /** Whether the gondola brakes are engaged (pods held) or released (pods swing free). */
  readonly gondolaBrakeEngaged: Signal<boolean> = computed(
    () => this.telemetry().gondolaBrakeEngaged,
  );

  /**
   * Whether the engine brake is engaged: drive power is cut and a strong
   * brake torque is applied, bringing the ride to a fast, complete stop.
   */
  readonly brakesEngaged: Signal<boolean> = computed(() => this.telemetry().brakesEngaged);

  /** Set the central mill power; value is clamped to 0–100 before dispatch. */
  setMillPower(value: number): void {
    this.source.applyCommand({ kind: 'set-mill-power', value: clampPower(value) });
  }

  /** Set the hub motor group power; value is clamped to 0–100 before dispatch. */
  setHubPower(value: number): void {
    this.source.applyCommand({ kind: 'set-hub-power', value: clampPower(value) });
  }

  /** Set the central mill rotation direction. */
  setMillDirection(direction: MotorDirection): void {
    this.source.applyCommand({ kind: 'set-mill-direction', direction });
  }

  /** Set the hub motor group rotation direction. */
  setHubDirection(direction: MotorDirection): void {
    this.source.applyCommand({ kind: 'set-hub-direction', direction });
  }

  /** Engage or release the gondola brakes. */
  setGondolaBrake(engaged: boolean): void {
    this.source.applyCommand({ kind: 'set-gondola-brake', engaged });
  }

  /**
   * Engage or release the engine brake. Engaging cuts mill and hub power to
   * zero and brings the ride to a fast, complete stop; releasing leaves
   * power at zero until the operator commands power again.
   */
  setEngineBrakes(engaged: boolean): void {
    this.source.applyCommand({ kind: 'brake-engines', engaged });
  }
}
