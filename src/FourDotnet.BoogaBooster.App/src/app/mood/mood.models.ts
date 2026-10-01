import { Gondola, SeatRider } from '../ride-dashboard/models/ride.models';

/** Happiness below this makes a guest "mad" (red head, counted in the queue). */
export const MAD_HAPPINESS = 30;

/** Nausea at or above this makes a rider "sick" (green head, may leave a puddle). */
export const SICK_NAUSEA = 70;

/** The ride's maximum G, in g; the upper end of the preferred-G scale. Mirrors `RideParameters.MaxGForce`. */
export const MAX_G_FORCE = 4.5;

/** Anything with a happiness value (queued guest, seated rider, offloaded rider). */
export interface HasHappiness {
  readonly happiness: number;
}

/** Anything with a nausea value. */
export interface HasNausea {
  readonly nausea: number;
}

/** Anything with a preferred-G value. */
export interface HasPreferredG {
  readonly preferredG: number;
}

/** Mean of `values`, or `null` when there are none (so "no data" is never shown as 0). */
export function average(values: readonly number[]): number | null {
  if (values.length === 0) {
    return null;
  }
  return values.reduce((total, value) => total + value, 0) / values.length;
}

/** Average happiness, `null` when the population is empty. */
export function averageHappiness(people: readonly HasHappiness[]): number | null {
  return average(people.map((person) => person.happiness));
}

/** Average preferred G in g, `null` when the population is empty. */
export function averagePreferredG(people: readonly HasPreferredG[]): number | null {
  return average(people.map((person) => person.preferredG));
}

/** Average nausea, `null` when the population is empty. */
export function averageNausea(people: readonly HasNausea[]): number | null {
  return average(people.map((person) => person.nausea));
}

/** Whether a guest is mad (happiness below {@link MAD_HAPPINESS}). */
export function isMad(person: HasHappiness): boolean {
  return person.happiness < MAD_HAPPINESS;
}

/** Whether a rider is sick (nausea at or above {@link SICK_NAUSEA}). */
export function isSick(person: HasNausea): boolean {
  return person.nausea >= SICK_NAUSEA;
}

/** Number of mad guests in the population. */
export function countMad(people: readonly HasHappiness[]): number {
  return people.filter(isMad).length;
}

/** Number of sick riders in the population. */
export function countSick(people: readonly HasNausea[]): number {
  return people.filter(isSick).length;
}

/** Every seated rider across all gondolas (empty seats skipped). */
export function seatedRiders(gondolas: readonly Gondola[]): readonly SeatRider[] {
  return gondolas.flatMap((gondola) =>
    gondola.seats.flatMap((seat) => (seat.rider ? [seat.rider] : [])),
  );
}
