/**
 * Domain models for the ride dashboard.
 *
 * The ride is one central mill rotating 16 gondolas mounted on 4 hubs (4
 * gondolas per hub). Each hub spins independently and carries a fixed number of
 * seats per gondola. These plain types are the contract shared by the
 * ride-state service, the telemetry source (simulated today, a real backend
 * feed later) and the presentational panels.
 */

/**
 * Overall lifecycle state of the ride, backed by the server-side state
 * machine (see `RideLifecycleService`). Order matches the backend's
 * `RideState` enum index (`0=Idle` … `6=EmergencyStop`).
 */
export type RideState =
  'idle' | 'loading' | 'safe' | 'started' | 'stopping' | 'offloading' | 'emergency-stop';

/** Direction a motor drives its rotation. */
export type MotorDirection = 'forward' | 'reverse';

/** State of a single passenger seat. */
export type SeatState = 'empty' | 'occupied-unsecured' | 'secured';

/** Overall security roll-up across every occupied seat. */
export type SecurityState = 'secured' | 'unsecured';

/** Overall rotational load-balance roll-up across every gondola. */
export type LoadBalanceState = 'safe' | 'unsafe';

/** Number of gondolas arranged around the central mill. */
export const GONDOLA_COUNT = 16;

/** Number of independently-driven hubs the gondolas are mounted on. */
export const HUB_COUNT = 4;

/** Number of gondolas carried by each hub. */
export const GONDOLAS_PER_HUB = GONDOLA_COUNT / HUB_COUNT;

/** Fixed number of seats per gondola. */
export const SEATS_PER_GONDOLA = 2;

/** Lowest allowed motor power, in percent. */
export const MIN_POWER = 0;

/** Highest allowed motor power, in percent. */
export const MAX_POWER = 100;

/**
 * Maximum allowed normalized rotational imbalance (see {@link loadEccentricity})
 * before the ride is considered off-balance. An evenly-spread load, or two
 * equal loads seated opposite each other, stays well under this threshold; a
 * load concentrated on one side of the mill exceeds it.
 */
export const MAX_SAFE_ECCENTRICITY = 0.35;

/** A single passenger seat. */
export interface Seat {
  readonly id: number;
  readonly state: SeatState;
  /** Weight measured by the seat's load cell, in kg (0 when empty). */
  readonly occupiedKg: number;
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
  /** Current angular position of the gondola around its hub, in degrees. */
  readonly angleDegrees: number;
}

/** A full snapshot of ride telemetry. */
export interface RideTelemetry {
  readonly state: RideState;
  /** The operator-triggerable target states legal right now. */
  readonly availableTransitions: readonly RideState[];
  readonly mill: Mill;
  readonly hubs: readonly Hub[];
  readonly gondolas: readonly Gondola[];
  /** Whether the gondola brakes are engaged (pods held) or released (pods swing free). */
  readonly gondolaBrakeEngaged: boolean;
}

/**
 * Raw wire shape of the lifecycle fields on `GET /api/ride/telemetry` that
 * {@link RideLifecycleService} cares about. `state`/`availableTransitions`
 * may arrive as numeric enum indices or as PascalCase names (System.Text.Json
 * default), so both are accepted here — see `toRideState`. The response also
 * carries `mill`/`hubs`/`gondolas`/`simulationTimeSeconds`/`isSafeToStart`/
 * `safetyReason`, which are not modelled here: the dashboard's physics
 * panels stay driven by the client simulator, not this feed.
 */
export interface RideTelemetryDto {
  readonly state: number | string;
  readonly availableTransitions: readonly (number | string)[];
}

/**
 * Highest power the central mill motor can draw, in watts. Mirrors the
 * backend's `RideParameters.MillMaxPowerWatts` — **must stay in sync** with
 * that value, since it is only used here to turn absolute watts back into a
 * percent for display/control.
 */
export const MILL_MAX_POWER_WATTS = 90000;

/**
 * Highest power a single hub motor can draw, in watts. Mirrors the backend's
 * `RideParameters.HubMaxPowerWatts` — **must stay in sync** with that value.
 */
export const HUB_MAX_POWER_WATTS = 15000;

/** Raw wire shape of the central mill within a `RideTelemetry` stream frame. */
export interface RideTelemetryMillStreamDto {
  readonly powerWatts: number;
  readonly rpm: number;
  readonly loadKg: number;
  readonly passengerLoadKg: number;
  readonly imbalanceMillimeters: number;
  readonly isBalanced: boolean;
  readonly isOverloaded: boolean;
}

/** Raw wire shape of a single hub within a `RideTelemetry` stream frame. */
export interface RideTelemetryHubStreamDto {
  readonly index: number;
  readonly powerWatts: number;
  readonly rpm: number;
  readonly loadKg: number;
}

/**
 * Raw wire shape of a single seat within a `RideTelemetry` stream frame.
 * `position`/`restraint` may arrive as a numeric enum index or a name (see
 * `SeatPosition`/`RestraintState` on the backend), so both are accepted.
 */
export interface RideTelemetrySeatStreamDto {
  readonly position: number | string;
  readonly occupiedKg: number;
  readonly restraint: number | string;
}

/**
 * Raw wire shape of a single gondola within a `RideTelemetry` stream frame.
 * `brake` may arrive as a numeric enum index or a name (see
 * `GondolaBrakeState` on the backend), so both are accepted.
 */
export interface RideTelemetryGondolaStreamDto {
  readonly hubIndex: number;
  readonly index: number;
  readonly brake: number | string;
  readonly angleDegrees: number;
  readonly rpm: number;
  readonly lateralG: number;
  readonly forwardG: number;
  readonly loadKg: number;
  readonly isSafeToDispatch: boolean;
  readonly seats: readonly RideTelemetrySeatStreamDto[];
}

/**
 * Full raw wire shape of one `RideTelemetry` snapshot, shared by
 * `GET /api/ride/telemetry` and each `GET /api/ride/telemetry/stream` frame.
 * `state`/`availableTransitions` may arrive as numeric enum indices or as
 * PascalCase names (System.Text.Json default), so both are accepted — see
 * `toRideState`. `isSafeToStart`/`safetyReason`/`simulationTimeSeconds` are
 * not modelled on the frontend yet.
 */
export interface RideTelemetryStreamDto {
  readonly state: number | string;
  readonly availableTransitions: readonly (number | string)[];
  readonly isSafeToStart: boolean;
  readonly safetyReason: number | string;
  readonly simulationTimeSeconds: number;
  readonly mill: RideTelemetryMillStreamDto;
  readonly hubs: readonly RideTelemetryHubStreamDto[];
  readonly gondolas: readonly RideTelemetryGondolaStreamDto[];
}

/** The backend's `GondolaBrakeState` names in index order (`0=Engaged, 1=Released`). */
const GONDOLA_BRAKE_NAMES = ['Engaged', 'Released'] as const;

/** The backend's `RestraintState` names in index order (`0=Open, 1=Closed, 2=Secured`). */
const RESTRAINT_NAMES = ['Open', 'Closed', 'Secured'] as const;

/** The backend's `SeatPosition` names in index order (`0=Left, 1=Right`). */
const SEAT_POSITION_NAMES = ['Left', 'Right'] as const;

/**
 * Resolves a numeric-or-string wire enum value to its zero-based index, given
 * the enum's names in index order. String values are matched
 * case-insensitively; an unrecognized value falls back to index `0`.
 */
function enumIndexOf(value: number | string, names: readonly string[]): number {
  if (typeof value === 'number') {
    return value;
  }
  const index = names.findIndex((name) => name.toLowerCase() === value.toLowerCase());
  return index === -1 ? 0 : index;
}

/** Whether the wire `GondolaBrakeState` value represents the engaged state. */
function brakeEngaged(value: number | string): boolean {
  return enumIndexOf(value, GONDOLA_BRAKE_NAMES) === 0;
}

/** Zero-based seat position (`0=Left, 1=Right`) from the wire `SeatPosition` value. */
function seatPositionIndex(value: number | string): number {
  return enumIndexOf(value, SEAT_POSITION_NAMES);
}

/**
 * A seat with no sensed weight is always `'empty'` regardless of restraint;
 * otherwise it is `'secured'` only when the wire `RestraintState` is
 * `Secured`, and `'occupied-unsecured'` for `Open`/`Closed`.
 */
function seatState(occupiedKg: number, restraint: number | string): SeatState {
  if (occupiedKg === 0) {
    return 'empty';
  }
  return enumIndexOf(restraint, RESTRAINT_NAMES) === 2 ? 'secured' : 'occupied-unsecured';
}

/**
 * Lossy conversion: the backend reports absolute motor power in watts, but
 * the UI models power as a percent of a fixed maximum. Rounds to the nearest
 * whole percent and clamps into `[0, 100]`.
 */
function fromWatts(watts: number, maxWatts: number): number {
  return clampPower(Math.round((watts / maxWatts) * 100));
}

/** Rounds `value` to `decimals` decimal places (0 by default). */
function round(value: number, decimals = 0): number {
  const factor = 10 ** decimals;
  return Math.round(value * factor) / factor;
}

/**
 * Maps one raw `RideTelemetry` wire snapshot (from the SSE stream or the
 * plain `GET`) onto the frontend's normalized {@link RideTelemetry}.
 *
 * Several conversions are deliberately lossy — see the comments below — since
 * the frontend models motor power as a percent and reports only two axes of
 * g-force, while the backend reports absolute watts and named force axes.
 */
export function mapRideTelemetry(dto: RideTelemetryStreamDto): RideTelemetry {
  return {
    state: toRideState(dto.state),
    availableTransitions: dto.availableTransitions.map(toRideState),
    mill: {
      power: fromWatts(dto.mill.powerWatts, MILL_MAX_POWER_WATTS),
      // The backend does not model a reverse direction for the mill motor;
      // it always spins one way, so this is always 'forward'.
      direction: 'forward',
      speedRpm: round(dto.mill.rpm, 2),
    },
    hubs: dto.hubs.map((hub) => ({
      id: hub.index + 1,
      power: fromWatts(hub.powerWatts, HUB_MAX_POWER_WATTS),
      // See the mill's direction above: hubs are not reversible either.
      direction: 'forward',
      speedRpm: round(hub.rpm, 2),
    })),
    gondolas: dto.gondolas.map((gondola) => ({
      id: gondola.hubIndex * GONDOLAS_PER_HUB + gondola.index + 1,
      angleDegrees: round(gondola.angleDegrees, 1),
      // The backend's `forwardG`/`lateralG` map onto the frontend's
      // `vertical`/`lateral` g-force axes respectively.
      gForce: { vertical: round(gondola.forwardG, 1), lateral: round(gondola.lateralG, 1) },
      seats: gondola.seats.map((seat) => ({
        id: seatPositionIndex(seat.position) + 1,
        occupiedKg: round(seat.occupiedKg),
        state: seatState(seat.occupiedKg, seat.restraint),
      })),
    })),
    // The backend engages/releases all gondola brakes together; the frontend
    // models one flag, so it is true only when every gondola reports engaged.
    gondolaBrakeEngaged: dto.gondolas.every((gondola) => brakeEngaged(gondola.brake)),
  };
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

/** Engage or release the gondola brakes. */
export interface SetGondolaBrakeCommand {
  readonly kind: 'set-gondola-brake';
  readonly engaged: boolean;
}

/** Request a ride lifecycle transition. */
export interface RequestStateTransitionCommand {
  readonly kind: 'request-state-transition';
  readonly state: RideState;
}

/** Cut mill and hub power so the ride coasts down. */
export interface BrakeEnginesCommand {
  readonly kind: 'brake-engines';
}

/** Any operator command the ride-state service can dispatch. */
export type RideCommand =
  | SetMillPowerCommand
  | SetHubPowerCommand
  | SetMillDirectionCommand
  | SetHubDirectionCommand
  | SetGondolaBrakeCommand
  | RequestStateTransitionCommand
  | BrakeEnginesCommand;

/** Pod motion values derived from the gondola brake state. */
export interface PodMotion {
  /** How much the pods hold against the combined rotation (0 = locked, 1 = free). */
  readonly freedom: number;
  /** How far the pods swing (0 = locked, 1 = full swing). */
  readonly swing: number;
}

/** Pod motion when the brakes are released — pods swing freely. */
export const POD_MOTION_RELEASED: PodMotion = { freedom: 1, swing: 1 };

/** Pod motion when the brakes are engaged — pods are held still. */
export const POD_MOTION_ENGAGED: PodMotion = { freedom: 0, swing: 0 };

/** Derive pod motion values from the gondola brake state. */
export function podMotionFor(brakeEngaged: boolean): PodMotion {
  return brakeEngaged ? POD_MOTION_ENGAGED : POD_MOTION_RELEASED;
}

/** Clamp a numeric value into the inclusive [min, max] range. */
export function clampPower(value: number, min = MIN_POWER, max = MAX_POWER): number {
  if (Number.isNaN(value)) {
    return min;
  }
  return Math.min(max, Math.max(min, value));
}

/** Total ride load, in kg: the sum of every seat's sensed weight. */
export function totalLoadKg(gondolas: readonly Gondola[]): number {
  return gondolas.reduce(
    (total, gondola) =>
      total + gondola.seats.reduce((seatTotal, seat) => seatTotal + seat.occupiedKg, 0),
    0,
  );
}

/**
 * Normalized rotational imbalance of the load around the central mill, in
 * `[0, 1]`. The 16 gondolas sit at even angular positions; for each gondola at
 * index `i` its angle is `(i / count) * 2π` and its weight is the sum of its
 * seats' `occupiedKg`. The eccentricity is the magnitude of the weight-vector
 * sum divided by the total weight — `0` for an evenly-spread (or empty) load,
 * approaching `1` as the load concentrates on one side of the mill.
 */
export function loadEccentricity(gondolas: readonly Gondola[]): number {
  let sumX = 0;
  let sumY = 0;
  let totalWeight = 0;

  gondolas.forEach((gondola, index) => {
    const weight = gondola.seats.reduce((seatTotal, seat) => seatTotal + seat.occupiedKg, 0);
    const angle = (index / gondolas.length) * 2 * Math.PI;
    sumX += weight * Math.cos(angle);
    sumY += weight * Math.sin(angle);
    totalWeight += weight;
  });

  if (totalWeight === 0) {
    return 0;
  }
  return Math.sqrt(sumX * sumX + sumY * sumY) / totalWeight;
}

/** Safe only when the load's rotational eccentricity is within tolerance. */
export function loadBalanceState(gondolas: readonly Gondola[]): LoadBalanceState {
  return loadEccentricity(gondolas) <= MAX_SAFE_ECCENTRICITY ? 'safe' : 'unsafe';
}

/**
 * The seven lifecycle states in the backend `RideState` enum's index order
 * (`0=Idle` … `6=EmergencyStop`); used to map a numeric wire value and to
 * enumerate every state (e.g. for the transition button grid).
 */
export const RIDE_STATE_BY_INDEX: readonly RideState[] = [
  'idle',
  'loading',
  'safe',
  'started',
  'stopping',
  'offloading',
  'emergency-stop',
];

/** The backend's PascalCase name for each lifecycle state. */
const RIDE_STATE_NAMES: Readonly<Record<RideState, string>> = {
  idle: 'Idle',
  loading: 'Loading',
  safe: 'Safe',
  started: 'Started',
  stopping: 'Stopping',
  offloading: 'Offloading',
  'emergency-stop': 'EmergencyStop',
};

/** Human-readable label for a lifecycle state, e.g. `'emergency-stop'` → `'Emergency stop'`. */
const RIDE_STATE_LABELS: Readonly<Record<RideState, string>> = {
  idle: 'Idle',
  loading: 'Loading',
  safe: 'Safe',
  started: 'Started',
  stopping: 'Stopping',
  offloading: 'Offloading',
  'emergency-stop': 'Emergency stop',
};

/**
 * Maps the server's numeric or string `RideState` value onto the frontend
 * union. Accepts a numeric enum index or a PascalCase name, matched
 * case-insensitively; falls back to `'idle'` for an unrecognized value.
 */
export function toRideState(value: number | string): RideState {
  if (typeof value === 'number') {
    return RIDE_STATE_BY_INDEX[value] ?? 'idle';
  }
  const normalized = value.toLowerCase();
  return (
    RIDE_STATE_BY_INDEX.find((state) => RIDE_STATE_NAMES[state].toLowerCase() === normalized) ??
    'idle'
  );
}

/** Maps a frontend lifecycle state back onto the backend's PascalCase name. */
export function toRideStateName(state: RideState): string {
  return RIDE_STATE_NAMES[state];
}

/** Human-friendly ride-state label, e.g. `'emergency-stop'` → `'Emergency stop'`. */
export function rideStateLabel(state: RideState): string {
  return RIDE_STATE_LABELS[state];
}
