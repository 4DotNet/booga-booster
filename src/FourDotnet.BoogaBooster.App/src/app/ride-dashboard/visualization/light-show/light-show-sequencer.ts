import { fract } from './bulb';
import { LIGHT_PATTERNS, LightPattern } from './light-patterns';

/** How many loops each pattern plays before the show moves to the next one. */
export const LOOPS_PER_PATTERN = 5;

/** Where the show is at a given moment. */
export interface SequencePosition {
  /** Index of the playing pattern in the pattern list. */
  readonly patternIndex: number;
  /** Which of its loops is playing, counted from 0. */
  readonly loop: number;
  /** Position within that loop, in [0, 1). */
  readonly phase: number;
}

/** Length of one full pass through every pattern, in seconds. */
export function showCycleSeconds(patterns: readonly LightPattern[] = LIGHT_PATTERNS): number {
  return patterns.reduce((sum, p) => sum + p.loopSeconds * LOOPS_PER_PATTERN, 0);
}

/**
 * The show position `elapsed` seconds after it started: pattern 0 plays its
 * {@link LOOPS_PER_PATTERN} loops, then pattern 1, …, and after the last pattern the
 * show wraps back to pattern 0, forever. Stateless, so it never drifts.
 */
export function sequenceAt(
  elapsed: number,
  patterns: readonly LightPattern[] = LIGHT_PATTERNS,
): SequencePosition {
  const cycle = showCycleSeconds(patterns);
  let t = Number.isFinite(elapsed) && elapsed > 0 ? elapsed % cycle : 0;
  for (let i = 0; i < patterns.length; i++) {
    const block = patterns[i].loopSeconds * LOOPS_PER_PATTERN;
    if (t < block) {
      const loops = t / patterns[i].loopSeconds;
      return { patternIndex: i, loop: Math.floor(loops), phase: fract(loops) };
    }
    t -= block;
  }
  // Only reachable through floating-point rounding at the very end of a cycle.
  return { patternIndex: 0, loop: 0, phase: 0 };
}
