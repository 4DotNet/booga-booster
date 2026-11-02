import { BULB_GROUP_KINDS, Bulb, BulbColour, describeBulb } from '../bulb';

/**
 * A spread of bulbs across every group kind, height and angle — enough to exercise
 * every branch of every pattern in the specs.
 */
export function sampleBulbs(): Bulb[] {
  const bulbs: Bulb[] = [];
  BULB_GROUP_KINDS.forEach((kind, group) => {
    const count = 9;
    for (let i = 0; i < count; i++) {
      const a = (i / count) * 2 * Math.PI + group;
      const r = 1 + group * 1.5;
      bulbs.push(
        describeBulb(
          r * Math.cos(a),
          0.3 + group * 1.3 + i * 0.1,
          r * Math.sin(a),
          i,
          count,
          group,
          kind,
          ((i + group) % 3) as BulbColour,
        ),
      );
    }
  });
  return bulbs;
}
