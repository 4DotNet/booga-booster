/**
 * Domain models for the ride dashboard.
 *
 * The ride is one central mill rotating 16 gondolas. Each gondola sits on a hub
 * that spins independently and carries a fixed number of seats. These plain
 * types are the contract shared by the ride-state service, the telemetry source
 * (simulated today, a real backend feed later) and the presentational panels.
 */

/** Overall operating state of the ride. */
export type RideState = 'stopped' | 'running' | 'emergency';

/** Direction a motor drives its rotation. */
export type MotorDirection = 'forward' | 'reverse';

/** State of a single passenger seat. */
export type SeatState = 'empty' | 'occupied-unsecured' | 'secured';

/** Overall security roll-up across every occupied seat. */
export type SecurityState = 'secured' | 'unsecured';

/** Number of gondolas arranged around the central mill. */
export const GONDOLA_COUNT = 16;

/** Fixed number of seats per gondola. */
export const SEATS_PER_GONDOLA = 4;

/** Lowest allowed motor power, in percent. */
export const MIN_POWER = 0;

/** Highest allowed motor power, in percent. */
export const MAX_POWER = 100;

/** A single passenger seat. */
export interface Seat {
  readonly id: number;
  readonly state: SeatState;
}

/**
 * G-forces experienced by a gondola, in g. Both values are signed: a positive
 * vertical force pushes riders back/up, a negative one pushes forward/down; a
 * positive lateral force pushes one way, a negative one the other.
 */
export interface GForce {
  readonly vertical: number;
  readonly lateral: number;
}

/** The central mill motor and its sensed rotation speed. */
export interface Mill {
  readonly power: number;
  readonly direction: MotorDirection;
  readonly speedRpm: number;
}

/** A single hub carrying one gondola; hub speeds vary with load. */
export interface Hub {
  readonly id: number;
  readonly power: number;
  readonly direction: MotorDirection;
  readonly speedRpm: number;
}

/** A gondola: the seats it carries and the g-forces it is experiencing. */
export interface Gondola {
  readonly id: number;
  readonly seats: readonly Seat[];
  readonly gForce: GForce;
}

/** A full snapshot of ride telemetry. */
export interface RideTelemetry {
  readonly state: RideState;
  readonly mill: Mill;
  readonly hubs: readonly Hub[];
  readonly gondolas: readonly Gondola[];
}

/** Set the central mill motor power (0–100). */
export interface SetMillPowerCommand {
  readonly kind: 'set-mill-power';
  readonly value: number;
}

/** Set the hub motor group power (0–100). */
export interface SetHubPowerCommand {
  readonly kind: 'set-hub-power';
  readonly value: number;
}

/** Set the central mill motor direction. */
export interface SetMillDirectionCommand {
  readonly kind: 'set-mill-direction';
  readonly direction: MotorDirection;
}

/** Set the hub motor group direction. */
export interface SetHubDirectionCommand {
  readonly kind: 'set-hub-direction';
  readonly direction: MotorDirection;
}

/** Any operator command the ride-state service can dispatch. */
export type RideCommand =
  | SetMillPowerCommand
  | SetHubPowerCommand
  | SetMillDirectionCommand
  | SetHubDirectionCommand;

/** Clamp a numeric value into the inclusive [min, max] range. */
export function clampPower(value: number, min = MIN_POWER, max = MAX_POWER): number {
  if (Number.isNaN(value)) {
    return min;
  }
  return Math.min(max, Math.max(min, value));
}
