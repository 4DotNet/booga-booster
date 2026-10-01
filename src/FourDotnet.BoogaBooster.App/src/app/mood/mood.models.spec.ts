import { Gondola } from '../ride-dashboard/models/ride.models';
import {
  MAD_HAPPINESS,
  MAX_G_FORCE,
  SICK_NAUSEA,
  average,
  averageHappiness,
  averageNausea,
  averagePreferredG,
  countMad,
  countSick,
  seatedRiders,
} from './mood.models';

const rider = (guestNumber: number, happiness: number, preferredG: number, nausea: number) => ({
  guestNumber,
  happiness,
  preferredG,
  nausea,
});

describe('mood.models', () => {
  it('exposes the agreed thresholds', () => {
    expect(MAD_HAPPINESS).toBe(30);
    expect(SICK_NAUSEA).toBe(70);
    expect(MAX_G_FORCE).toBe(4.5);
  });

  it('averages values and returns null for an empty population', () => {
    expect(average([60, 80])).toBe(70);
    expect(average([])).toBeNull();
    expect(averageHappiness([])).toBeNull();
    expect(averagePreferredG([])).toBeNull();
    expect(averageNausea([])).toBeNull();
  });

  it('averages each mood dimension', () => {
    const riders = [rider(1, 60, 3, 10), rider(2, 80, 3.6, 30)];

    expect(averageHappiness(riders)).toBe(70);
    expect(averagePreferredG(riders)).toBeCloseTo(3.3, 5);
    expect(averageNausea(riders)).toBe(20);
  });

  it('counts mad (< 30) and sick (>= 70) with strict/inclusive boundaries', () => {
    const people = [rider(1, 29, 1, 69), rider(2, 30, 1, 70), rider(3, 0, 1, 100)];

    expect(countMad(people)).toBe(2);
    expect(countSick(people)).toBe(2);
    expect(countMad([])).toBe(0);
  });

  it('collects seated riders from gondolas, skipping empty seats', () => {
    const gondolas: Gondola[] = [
      {
        id: 1,
        angleDegrees: 0,
        gForce: { vertical: 0, lateral: 0 },
        seats: [
          { id: 1, state: 'secured', occupiedKg: 70, rider: rider(1, 50, 2, 0) },
          { id: 2, state: 'empty', occupiedKg: 0, rider: null },
        ],
      },
      {
        id: 2,
        angleDegrees: 0,
        gForce: { vertical: 0, lateral: 0 },
        seats: [{ id: 1, state: 'secured', occupiedKg: 70 }],
      },
    ];

    expect(seatedRiders(gondolas).map((r) => r.guestNumber)).toEqual([1]);
  });
});
