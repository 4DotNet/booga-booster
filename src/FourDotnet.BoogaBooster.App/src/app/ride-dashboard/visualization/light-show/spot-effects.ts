import { clamp01, fract } from './bulb';

/** Length of one spotlight routine, in seconds. */
export const SPOT_CYCLE_SECONDS = 12;

/** Blink rate in the routine's blink section, in flashes per second (WCAG cap is 3). */
export const SPOT_BLINK_HZ = 1.5;

/** Brightness a spot never drops below, so the ride never goes fully dark. */
const SPOT_FLOOR = 0.12;

/** Smooth cosine fade between 0 (u = 0) and 1 (u = 1). */
function ease(u: number): number {
  return 0.5 - 0.5 * Math.cos(Math.PI * clamp01(u));
}

/**
 * Brightness in [0, 1] of pole `pole` (of `poleCount`) `elapsed` seconds into the
 * show. Each pole runs the same 12 s routine — two slow breaths, a blink section,
 * then a fade back down — shifted by its share of the cycle, so the poles fade and
 * blink out of step and the light seems to roam around the ride.
 */
export function spotBrightness(pole: number, poleCount: number, elapsed: number): number {
  const offset = poleCount > 0 ? pole / poleCount : 0;
  const safeElapsed = Number.isFinite(elapsed) ? elapsed : 0;
  const u = fract(safeElapsed / SPOT_CYCLE_SECONDS + offset);
  const t = u * SPOT_CYCLE_SECONDS;

  let level: number;
  if (t < 6) {
    // two slow breaths: 0 → 1 → 0 every 3 s
    level = 0.5 - 0.5 * Math.cos((2 * Math.PI * t) / 3);
  } else if (t < 6.5) {
    level = ease((t - 6) / 0.5); // fade up into the blinks
  } else if (t < 9.5) {
    const blinkPhase = fract((t - 6.5) * SPOT_BLINK_HZ);
    level = blinkPhase < 0.5 ? 1 : 0.15;
  } else if (t < 10.5) {
    level = 1; // hold full
  } else {
    level = 1 - ease((t - 10.5) / 1.5); // fade down to the start of the cycle
  }
  return SPOT_FLOOR + (1 - SPOT_FLOOR) * clamp01(level);
}
