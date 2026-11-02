import {
  BULB_GROUP_KINDS,
  Bulb,
  BulbColour,
  SCENE_HEIGHT,
  SCENE_RADIUS,
  clamp01,
  fract,
} from './bulb';

/**
 * One bulb animation. `brightness` (and the optional `colour`) are pure functions of
 * the bulb and the `phase` within one loop, `phase` ∈ [0, 1). Brightness is in [0, 1].
 *
 * Every pattern keeps each bulb below 3 flashes per second (WCAG 2.3.1); the flash
 * rate is checked by `light-show-safety.spec.ts`.
 */
export interface LightPattern {
  readonly name: string;
  /** Duration of one loop, in seconds. */
  readonly loopSeconds: number;
  brightness(bulb: Bulb, phase: number): number;
  /** Overrides the bulb's designed colour while the pattern plays. */
  colour?(bulb: Bulb, phase: number): BulbColour;
}

/** Brightness of a bulb that is "off" in an on/off pattern: a faint glow, not black. */
const DIM = 0.1;

/** Smooth 0→1→0 bump of half-width `width` centred on 0 (distance `d` ≥ 0). */
function bump(d: number, width: number): number {
  const t = clamp01(1 - d / width);
  return t * t * (3 - 2 * t);
}

/** Shortest distance between two values on a unit circle ([0, 1) wraps). */
function circularDistance(a: number, b: number): number {
  const d = Math.abs(fract(a - b));
  return Math.min(d, 1 - d);
}

/** Lifts a 0..1 level above the dim floor. */
function lit(level: number): number {
  return DIM + (1 - DIM) * clamp01(level);
}

/** Marquee chase: every third bulb lit, stepping along each string. */
const chase: LightPattern = {
  name: 'chase',
  loopSeconds: 3,
  brightness: (b, phase) => ((b.index + Math.floor(phase * 12)) % 3 === 0 ? 1 : DIM),
};

/** Even and odd bulbs swap, twice per loop. */
const alternate: LightPattern = {
  name: 'alternate',
  loopSeconds: 2,
  brightness: (b, phase) => ((b.index + Math.floor(phase * 2)) % 2 === 0 ? 1 : DIM),
};

/** Rings of light travel outwards from the centre of the ride. */
const radialWave: LightPattern = {
  name: 'radial-wave',
  loopSeconds: 3,
  brightness: (b, phase) => {
    const wave = fract(b.distance / (SCENE_RADIUS / 2) - phase * 2);
    return lit(bump(Math.min(wave, 1 - wave), 0.18));
  },
};

/** A lighthouse beam sweeps once around the ride per loop. */
const sweep: LightPattern = {
  name: 'sweep',
  loopSeconds: 4,
  brightness: (b, phase) => lit(bump(circularDistance(b.angle, phase), 0.14)),
};

/** A band of light climbs from the floor to the top of the backdrop. */
const rise: LightPattern = {
  name: 'rise',
  loopSeconds: 3,
  brightness: (b, phase) => {
    const h = clamp01(b.height / SCENE_HEIGHT);
    return lit(bump(Math.abs(h - phase * 1.3 + 0.15), 0.18));
  },
};

/** Random sparkles over a soft glow: each bulb glints twice per loop. */
const twinkle: LightPattern = {
  name: 'twinkle',
  loopSeconds: 4,
  brightness: (b, phase) => {
    const t = fract(b.seed * 7.31 + phase * 2);
    return t < 0.18 ? 0.3 + 0.7 * bump(Math.abs(t - 0.09), 0.09) : 0.3;
  },
};

/** Every bulb breathes in and out together. */
const breathe: LightPattern = {
  name: 'breathe',
  loopSeconds: 4,
  brightness: (_b, phase) => lit(0.5 - 0.5 * Math.cos(2 * Math.PI * phase)),
};

/** A bright head with a fading tail runs along each string. */
const comet: LightPattern = {
  name: 'comet',
  loopSeconds: 2.5,
  brightness: (b, phase) => {
    const behind = fract(phase * 1.25 - b.order);
    return behind < 0.3 ? lit(1 - behind / 0.3) : DIM;
  },
};

/** The ride fills with light from the crown outwards to the backdrop, then resets. */
const groupCascade: LightPattern = {
  name: 'group-cascade',
  loopSeconds: 4,
  brightness: (b, phase) => {
    const stage = BULB_GROUP_KINDS.indexOf(b.kind);
    const reached = phase * (BULB_GROUP_KINDS.length + 2);
    return reached >= stage + 1 ? 1 : lit(clamp01(reached - stage));
  },
};

/** Lub-dub: a strong and a softer pulse, then rest. */
const heartbeat: LightPattern = {
  name: 'heartbeat',
  loopSeconds: 1.6,
  brightness: (_b, phase) =>
    lit(Math.max(bump(Math.abs(phase - 0.12), 0.1), 0.8 * bump(Math.abs(phase - 0.36), 0.1))),
};

/** Light splits from the centre line and runs out to both sides. */
const split: LightPattern = {
  name: 'split',
  loopSeconds: 3,
  brightness: (b, phase) => lit(bump(Math.abs(b.lateral - phase * 1.2 + 0.1), 0.15)),
};

/** All bulbs bright while bands of warm, pink and cyan rotate around the ride. */
const colourCycle: LightPattern = {
  name: 'colour-cycle',
  loopSeconds: 6,
  brightness: () => 0.9,
  colour: (b, phase) => (Math.floor(fract(b.angle * 3 + phase) * 3) % 3) as BulbColour,
};

/** The twelve patterns, in show order. */
export const LIGHT_PATTERNS: readonly LightPattern[] = [
  chase,
  alternate,
  radialWave,
  sweep,
  rise,
  twinkle,
  breathe,
  comet,
  groupCascade,
  heartbeat,
  split,
  colourCycle,
];
