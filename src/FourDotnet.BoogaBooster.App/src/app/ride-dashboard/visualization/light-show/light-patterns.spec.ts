import { describeBulb } from './bulb';
import { LIGHT_PATTERNS } from './light-patterns';
import { sampleBulbs } from './testing/sample-bulbs';

describe('LIGHT_PATTERNS', () => {
  const bulbs = sampleBulbs();
  const phases = Array.from({ length: 41 }, (_, i) => i / 41);

  it('offers twelve uniquely named patterns with positive loop durations', () => {
    expect(LIGHT_PATTERNS).toHaveLength(12);
    expect(new Set(LIGHT_PATTERNS.map((p) => p.name)).size).toBe(12);
    for (const p of LIGHT_PATTERNS) {
      expect(p.loopSeconds).toBeGreaterThan(0);
    }
  });

  it.each(LIGHT_PATTERNS.map((p) => [p.name, p] as const))(
    '%s keeps every brightness within [0, 1]',
    (_name, pattern) => {
      for (const bulb of bulbs) {
        for (const phase of phases) {
          const b = pattern.brightness(bulb, phase);
          expect(b).toBeGreaterThanOrEqual(0);
          expect(b).toBeLessThanOrEqual(1);
        }
      }
    },
  );

  it.each(LIGHT_PATTERNS.map((p) => [p.name, p] as const))(
    '%s is deterministic and actually animates',
    (_name, pattern) => {
      const seen = new Set<string>();
      for (const phase of phases) {
        const frame = bulbs.map((bulb) => {
          const b = pattern.brightness(bulb, phase);
          expect(pattern.brightness(bulb, phase)).toBe(b);
          const c = pattern.colour?.(bulb, phase) ?? bulb.colour;
          expect(pattern.colour?.(bulb, phase) ?? bulb.colour).toBe(c);
          return `${b.toFixed(3)}:${c}`;
        });
        seen.add(frame.join('|'));
      }
      expect(seen.size).toBeGreaterThan(1);
    },
  );

  it('recolours only with designed colours', () => {
    const cycle = LIGHT_PATTERNS.find((p) => p.colour);
    expect(cycle).toBeDefined();
    for (const bulb of bulbs) {
      for (const phase of phases) {
        expect([0, 1, 2]).toContain(cycle!.colour!(bulb, phase));
      }
    }
  });

  it('describes a bulb from its rest-pose position', () => {
    const bulb = describeBulb(0, 2, 5, 3, 7, 4, 'hub', 1);

    expect(bulb.angle).toBeCloseTo(0.25, 5);
    expect(bulb.distance).toBeCloseTo(5, 5);
    expect(bulb.height).toBe(2);
    expect(bulb.lateral).toBe(0);
    expect(bulb.order).toBeCloseTo(0.5, 5);
    expect(bulb.seed).toBeGreaterThanOrEqual(0);
    expect(bulb.seed).toBeLessThan(1);
    expect(describeBulb(1, 0, 0, 0, 1, 0, 'base', 0).order).toBe(0);
  });
});
