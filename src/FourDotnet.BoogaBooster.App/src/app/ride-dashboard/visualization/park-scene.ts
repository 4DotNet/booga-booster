/**
 * Pure logic for the park scene around the ride: where guests queue, how they
 * walk, what colour they are and where puddles go. No Three.js in here, so it
 * is fully unit-testable; `ride-visualization.ts` only wires it to the WebGL
 * scene. All coordinates are metres on the ground plane (`x`, `z`), the rig
 * standing at the origin.
 */

import { MAD_HAPPINESS, SICK_NAUSEA } from '../../mood/mood.models';

/** A point on the ground plane. */
export interface Vec2 {
  readonly x: number;
  readonly z: number;
}

/** Distance of both booths from the centre; beyond the 9 m sweep (6 + 2 + 1). */
export const BOOTH_DISTANCE = 12;

/** Entry booth position, on the +x side. */
export const ENTRY_BOOTH: Vec2 = { x: BOOTH_DISTANCE, z: 0 };

/** Exit booth position, 90 degrees round from the entry. */
export const EXIT_BOOTH: Vec2 = { x: 0, z: BOOTH_DISTANCE };

/** Rows of the queue switchback. */
export const QUEUE_ROWS = 4;

/** Slots per switchback row. */
export const SLOTS_PER_ROW = 15;

/** Number of guests that can be drawn in the queue. */
export const QUEUE_CAPACITY = QUEUE_ROWS * SLOTS_PER_ROW;

/** Distance between neighbouring guests in a row. */
export const SLOT_SPACING = 0.6;

/** Distance between switchback rows. */
export const ROW_SPACING = 1;

/** x of the first row: just behind the entry booth. */
const QUEUE_START_X = ENTRY_BOOTH.x + 1.4;

/** Walking speed in metres per second. */
export const WALK_SPEED = 1.4;

/** Distance an exiting rider walks away from the exit booth. */
export const EXIT_WALK_DISTANCE = 10;

/** Seconds a puddle takes to dry up. */
export const PUDDLE_LIFETIME_S = 20;

/** The shirt palette; a guest wears `SHIRT_PALETTE[guestNumber mod 8]`. */
export const SHIRT_PALETTE: readonly number[] = [
  0xe53935, 0x1e88e5, 0x43a047, 0xfb8c00, 0x8e24aa, 0x00acc1, 0xf06292, 0xeceff1,
];

/** Head colours. */
export const HEAD_GREEN = 0x4caf50;
export const HEAD_RED = 0xe53935;
export const HEAD_LEGO_YELLOW = 0xf2cd37;

/** A walking figure; `leaving` figures are removed once they reach their target. */
export interface Figure {
  readonly guestNumber: number;
  readonly position: Vec2;
  readonly target: Vec2;
  readonly leaving: boolean;
  readonly happiness: number;
  readonly nausea: number;
}

/** What the scene needs to know about a guest to draw them. */
export interface GuestMood {
  readonly guestNumber: number;
  readonly happiness: number;
  readonly nausea: number;
}

/** A rider leaving the ride; `guestNumber` is `null` for a hand-boarded rider. */
export interface LeavingRider {
  readonly guestNumber: number | null;
  readonly happiness: number;
  readonly nausea: number;
}

/**
 * A stable identity for drawing: the guest number, or for a rider without one
 * a synthetic negative number derived from `fallbackIndex` (so colour and
 * puddle placement stay deterministic).
 */
export function figureId(guestNumber: number | null, fallbackIndex: number): number {
  return guestNumber ?? -(fallbackIndex + 1);
}

/** Position of queue slot `index` (0 = front, next to the entry booth), serpentine over the rows. */
export function queueSlotPosition(index: number): Vec2 {
  const row = Math.floor(index / SLOTS_PER_ROW);
  const column = index % SLOTS_PER_ROW;
  const along = row % 2 === 0 ? column : SLOTS_PER_ROW - 1 - column;
  return { x: QUEUE_START_X + row * ROW_SPACING, z: along * SLOT_SPACING };
}

/** Where new guests appear: just past the far end of the path. */
export function queueSpawnPosition(): Vec2 {
  const end = queueSlotPosition(QUEUE_CAPACITY - 1);
  return { x: end.x + ROW_SPACING, z: end.z };
}

/** Number of queued guests that do not fit on the path (the `+N` label). */
export function overflowCount(queued: number): number {
  return Math.max(0, queued - QUEUE_CAPACITY);
}

/** Shirt colour for a guest: stable per guest number. */
export function shirtColor(guestNumber: number): number {
  const index =
    ((guestNumber % SHIRT_PALETTE.length) + SHIRT_PALETTE.length) % SHIRT_PALETTE.length;
  return SHIRT_PALETTE[index];
}

/** Head colour: green when sick, else red when mad, else LEGO yellow. */
export function headColor(mood: { happiness: number; nausea: number }): number {
  if (mood.nausea >= SICK_NAUSEA) {
    return HEAD_GREEN;
  }
  return mood.happiness < MAD_HAPPINESS ? HEAD_RED : HEAD_LEGO_YELLOW;
}

/** Moves `from` towards `to` by at most `distance`, never overshooting. */
export function walkStep(from: Vec2, to: Vec2, distance: number): Vec2 {
  const dx = to.x - from.x;
  const dz = to.z - from.z;
  const remaining = Math.hypot(dx, dz);
  if (remaining <= distance || remaining === 0) {
    return to;
  }
  const t = distance / remaining;
  return { x: from.x + dx * t, z: from.z + dz * t };
}

/** Whether a figure stands on its target. */
export function hasArrived(figure: Figure): boolean {
  return figure.position.x === figure.target.x && figure.position.z === figure.target.z;
}

/**
 * Reassigns every figure's target after a queue update. `queue` is the whole
 * queue in order (front first); the first {@link QUEUE_CAPACITY} get a slot by
 * position. Known guests keep walking from where they are, new guests appear at
 * the far end, and figures that left the queue head for the entry booth.
 * With `instant` (reduced motion) nobody walks: positions snap to targets.
 */
export function assignQueueTargets(
  figures: readonly Figure[],
  queue: readonly GuestMood[],
  instant: boolean,
): Figure[] {
  const known = new Map(figures.map((figure) => [figure.guestNumber, figure]));
  const visible = queue.slice(0, QUEUE_CAPACITY);
  const visibleNumbers = new Set(visible.map((guest) => guest.guestNumber));

  const queued = visible.map((guest, index): Figure => {
    const target = queueSlotPosition(index);
    const position = instant
      ? target
      : (known.get(guest.guestNumber)?.position ?? queueSpawnPosition());
    return {
      guestNumber: guest.guestNumber,
      position,
      target,
      leaving: false,
      happiness: guest.happiness,
      nausea: guest.nausea,
    };
  });

  const departing = figures
    .filter((figure) => !visibleNumbers.has(figure.guestNumber))
    .map((figure): Figure => {
      // Leaving figures (boarding guests and exit walkers) keep their own target.
      if (figure.leaving) {
        return figure;
      }
      return instant
        ? { ...figure, leaving: true, position: ENTRY_BOOTH, target: ENTRY_BOOTH }
        : { ...figure, leaving: true, target: ENTRY_BOOTH };
    });

  return [...queued, ...departing];
}

/** Advances every figure by `dt` seconds of walking and drops leavers that have arrived. */
export function advanceFigures(figures: readonly Figure[], dt: number): Figure[] {
  return figures
    .map((figure) => ({
      ...figure,
      position: walkStep(figure.position, figure.target, WALK_SPEED * dt),
    }))
    .filter((figure) => !(figure.leaving && hasArrived(figure)));
}

/** The point an exit walker heads for: straight away from the exit booth, and from the rig. */
export function exitWalkTarget(lateral: number): Vec2 {
  return { x: EXIT_BOOTH.x + lateral, z: EXIT_BOOTH.z + EXIT_WALK_DISTANCE };
}

/** Sideways offset keeping simultaneously exiting riders from overlapping. */
export function exitLateralOffset(slot: number): number {
  return ((slot % 5) - 2) * 0.5;
}

/** Creates the walkers for an offload: each starts at the exit booth and walks away. */
export function spawnExitWalkers(riders: readonly LeavingRider[], instant: boolean): Figure[] {
  return riders.map((rider, slot): Figure => {
    const lateral = exitLateralOffset(slot);
    const target = exitWalkTarget(lateral);
    return {
      guestNumber: figureId(rider.guestNumber, slot),
      position: instant ? target : { x: EXIT_BOOTH.x + lateral, z: EXIT_BOOTH.z },
      target,
      leaving: true,
      happiness: rider.happiness,
      nausea: rider.nausea,
    };
  });
}

/** A puddle left by a sick rider; `age` is in seconds. */
export interface Puddle {
  readonly guestNumber: number;
  readonly position: Vec2;
  readonly age: number;
}

/** Deterministic pseudo-random value in [0, 1) from a guest number and a salt. */
function unitHash(guestNumber: number, salt: number): number {
  let h = Math.imul(guestNumber + 1, 2654435761) ^ Math.imul(salt + 1, 40503);
  h = Math.imul(h ^ (h >>> 15), 2246822519);
  h ^= h >>> 13;
  return (h >>> 0) / 4294967296;
}

/** Puddle location for a guest: a fixed spot along the exit path (same guest, same spot). */
export function puddlePosition(guestNumber: number): Vec2 {
  return {
    x: EXIT_BOOTH.x + (unitHash(guestNumber, 1) - 0.5) * 2,
    z: EXIT_BOOTH.z + 2 + unitHash(guestNumber, 2) * (EXIT_WALK_DISTANCE - 3),
  };
}

/** Puddles for the riders of an offload that left sick. */
export function spawnPuddles(riders: readonly LeavingRider[]): Puddle[] {
  return riders
    .map((rider, slot) => ({ id: figureId(rider.guestNumber, slot), nausea: rider.nausea }))
    .filter((rider) => rider.nausea >= SICK_NAUSEA)
    .map((rider) => ({
      guestNumber: rider.id,
      position: puddlePosition(rider.id),
      age: 0,
    }));
}

/** Puddle size factor: 1 when fresh, shrinking linearly to 0 over {@link PUDDLE_LIFETIME_S}. */
export function puddleScale(age: number): number {
  return Math.min(1, Math.max(0, 1 - age / PUDDLE_LIFETIME_S));
}

/** Ages puddles by `dt` seconds and removes those that dried up. */
export function agePuddles(puddles: readonly Puddle[], dt: number): Puddle[] {
  return puddles
    .map((puddle) => ({ ...puddle, age: puddle.age + dt }))
    .filter((puddle) => puddleScale(puddle.age) > 0);
}

/**
 * What an offload counter change means. The first counter ever seen is only a
 * baseline (a page reload mid-session must not replay the last offload); only
 * an increase afterwards is a new offload. A decrease (backend restart)
 * re-baselines without reacting.
 */
export function offloadReaction(
  seen: number | null,
  counter: number,
): { readonly spawn: boolean; readonly seen: number } {
  return { spawn: seen !== null && counter > seen, seen: counter };
}

/** Telemetry gondola id (`hubIndex * 4 + index + 1`) to the scene's gondola array index `k * 4 + j`. */
export function gondolaSceneIndex(gondolaId: number): number {
  return gondolaId - 1;
}

/** Local offset of a seat's rider within the gondola pivot: seat 0 left, seat 1 right. */
export function seatOffset(seatIndex: number): { x: number; y: number; z: number } {
  return { x: seatIndex === 0 ? -0.45 : 0.45, y: 0.2, z: 0 };
}
