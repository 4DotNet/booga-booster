import { Bulb, BulbColour, clamp01 } from './bulb';
import { LIGHT_PATTERNS, LightPattern } from './light-patterns';
import { SequencePosition, sequenceAt } from './light-show-sequencer';

/** Brightness every bulb and spot holds when the operator asks for reduced motion. */
export const STEADY_BRIGHTNESS = 0.85;

/**
 * Evaluates the show for every bulb at `elapsed` seconds since it started, writing
 * each bulb's brightness and colour into the given buffers (index-aligned with
 * `bulbs`). Under reduced motion every bulb holds {@link STEADY_BRIGHTNESS} in its
 * designed colour and no sequence runs.
 *
 * @returns the playing position, or `null` under reduced motion.
 */
export function evaluateShow(
  bulbs: readonly Bulb[],
  elapsed: number,
  reducedMotion: boolean,
  brightnessOut: Float32Array,
  colourOut: Uint8Array,
  patterns: readonly LightPattern[] = LIGHT_PATTERNS,
): SequencePosition | null {
  if (reducedMotion) {
    for (let i = 0; i < bulbs.length; i++) {
      brightnessOut[i] = STEADY_BRIGHTNESS;
      colourOut[i] = bulbs[i].colour;
    }
    return null;
  }
  const position = sequenceAt(elapsed, patterns);
  const pattern = patterns[position.patternIndex];
  for (let i = 0; i < bulbs.length; i++) {
    const bulb = bulbs[i];
    brightnessOut[i] = clamp01(pattern.brightness(bulb, position.phase));
    colourOut[i] = pattern.colour
      ? (pattern.colour(bulb, position.phase) satisfies BulbColour)
      : bulb.colour;
  }
  return position;
}
