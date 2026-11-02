import { SPOT_BLINK_HZ, SPOT_CYCLE_SECONDS, spotBrightness } from './spot-effects';

describe('spotBrightness', () => {
  const samples = (pole: number) =>
    Array.from({ length: 480 }, (_, i) => spotBrightness(pole, 8, (i / 480) * SPOT_CYCLE_SECONDS));

  it('stays within [0, 1] and never goes fully dark', () => {
    for (let pole = 0; pole < 8; pole++) {
      for (const b of samples(pole)) {
        expect(b).toBeGreaterThan(0.1);
        expect(b).toBeLessThanOrEqual(1);
      }
    }
  });

  it('fades and blinks between dim and full over a cycle', () => {
    const s = samples(0);
    expect(Math.min(...s)).toBeLessThan(0.2);
    expect(Math.max(...s)).toBeCloseTo(1, 5);
    // the blink section holds full and dim levels with hard edges
    const jumps = s.filter((b, i) => i > 0 && Math.abs(b - s[i - 1]) > 0.5).length;
    expect(jumps).toBeGreaterThanOrEqual(4);
  });

  it('runs the poles out of step', () => {
    let differing = 0;
    for (let i = 0; i < 100; i++) {
      const t = i * 0.37;
      if (Math.abs(spotBrightness(0, 8, t) - spotBrightness(3, 8, t)) > 0.05) {
        differing++;
      }
    }
    expect(differing).toBeGreaterThan(60);
  });

  it('repeats every cycle and tolerates a non-finite time', () => {
    expect(spotBrightness(2, 8, 3.3)).toBeCloseTo(
      spotBrightness(2, 8, 3.3 + SPOT_CYCLE_SECONDS),
      6,
    );
    expect(spotBrightness(0, 8, NaN)).toBe(spotBrightness(0, 8, 0));
    expect(spotBrightness(0, 0, 1)).toBe(spotBrightness(0, 1, 1));
    expect(SPOT_BLINK_HZ).toBeLessThanOrEqual(3);
  });
});
