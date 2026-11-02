import { LIGHT_PATTERNS } from './light-patterns';
import { LOOPS_PER_PATTERN, sequenceAt, showCycleSeconds } from './light-show-sequencer';
import { STEADY_BRIGHTNESS, evaluateShow } from './light-show';
import { sampleBulbs } from './testing/sample-bulbs';

describe('light show sequencer', () => {
  const block = (i: number) => LIGHT_PATTERNS[i].loopSeconds * LOOPS_PER_PATTERN;
  const startOf = (i: number) => LIGHT_PATTERNS.slice(0, i).reduce((s, _p, k) => s + block(k), 0);

  it('starts with the first pattern in its first loop', () => {
    expect(sequenceAt(0)).toEqual({ patternIndex: 0, loop: 0, phase: 0 });
  });

  it('plays five loops of a pattern before moving on', () => {
    const loop = LIGHT_PATTERNS[0].loopSeconds;
    expect(sequenceAt(loop * 4.5)).toEqual(expect.objectContaining({ patternIndex: 0, loop: 4 }));
    expect(sequenceAt(loop * 4.5).phase).toBeCloseTo(0.5, 5);
    expect(sequenceAt(block(0) - 1e-6).patternIndex).toBe(0);
  });

  it('switches to the next pattern after the fifth loop', () => {
    const next = sequenceAt(block(0) + 1e-6);
    expect(next.patternIndex).toBe(1);
    expect(next.loop).toBe(0);
    expect(sequenceAt(startOf(7) + 1e-6).patternIndex).toBe(7);
  });

  it('wraps to the first pattern after the twelfth', () => {
    const cycle = showCycleSeconds();
    expect(cycle).toBeCloseTo(startOf(12), 6);
    expect(sequenceAt(cycle - 1e-6).patternIndex).toBe(11);
    expect(sequenceAt(cycle + 1e-6).patternIndex).toBe(0);
    expect(sequenceAt(cycle * 7 + startOf(3) + 0.01).patternIndex).toBe(3);
  });

  it('treats a non-finite or negative elapsed time as the start', () => {
    expect(sequenceAt(NaN)).toEqual({ patternIndex: 0, loop: 0, phase: 0 });
    expect(sequenceAt(-4)).toEqual({ patternIndex: 0, loop: 0, phase: 0 });
  });

  describe('evaluateShow', () => {
    const bulbs = sampleBulbs();
    const brightness = new Float32Array(bulbs.length);
    const colours = new Uint8Array(bulbs.length);

    it('writes the playing pattern for every bulb', () => {
      const position = evaluateShow(bulbs, startOf(11) + 1, false, brightness, colours);

      expect(position?.patternIndex).toBe(11);
      expect(Array.from(brightness).every((b) => b >= 0 && b <= 1)).toBe(true);
      const pattern = LIGHT_PATTERNS[11];
      expect(colours[5]).toBe(pattern.colour!(bulbs[5], position!.phase));
    });

    it('keeps the designed colour when the pattern does not recolour', () => {
      evaluateShow(bulbs, 0.4, false, brightness, colours);
      expect(Array.from(colours)).toEqual(bulbs.map((b) => b.colour));
    });

    it('holds a steady glow under reduced motion', () => {
      const a = evaluateShow(bulbs, 1, true, brightness, colours);
      const first = Array.from(brightness);
      evaluateShow(bulbs, 37, true, brightness, colours);

      expect(a).toBeNull();
      expect(first.every((b) => Math.abs(b - STEADY_BRIGHTNESS) < 1e-6)).toBe(true);
      expect(Array.from(brightness)).toEqual(first);
    });
  });
});
