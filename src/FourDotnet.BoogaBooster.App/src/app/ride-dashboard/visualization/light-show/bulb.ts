/** Designed bulb colours, in the order the web-model export encodes them. */
export const BULB_COLOURS = ['warm', 'pink', 'cyan'] as const;

/** Index into {@link BULB_COLOURS}: 0 warm white, 1 pink, 2 cyan. */
export type BulbColour = 0 | 1 | 2;

/** Where on the ride a bulb string sits — ordered from the centre outwards. */
export const BULB_GROUP_KINDS = [
  'crown',
  'base',
  'main-arm',
  'hub',
  'hub-arm',
  'gondola',
  'pole',
  'backdrop',
] as const;

export type BulbGroupKind = (typeof BULB_GROUP_KINDS)[number];

/** Radius (metres) of the half-round backdrop: the outermost bulbs. */
export const SCENE_RADIUS = 13;

/** Height (metres) of the backdrop crest: the highest bulbs. */
export const SCENE_HEIGHT = 11.2;

/**
 * Everything a light pattern may know about one bulb. Positions are taken in the
 * rig's rest pose, so a pattern reads the same while the rig turns.
 */
export interface Bulb {
  /** Angle around the ride centre, normalised to [0, 1). */
  readonly angle: number;
  /** Horizontal distance from the ride's axis, in metres. */
  readonly distance: number;
  /** Height above the floor, in metres. */
  readonly height: number;
  /** Sideways offset from the ride's front-to-back centre line, normalised to [0, 1]. */
  readonly lateral: number;
  /** Position along its string, normalised to [0, 1]. */
  readonly order: number;
  /** Position along its string, counted from 0. */
  readonly index: number;
  /** Which string (group) the bulb belongs to, counted from 0. */
  readonly group: number;
  readonly kind: BulbGroupKind;
  readonly colour: BulbColour;
  /** A stable pseudo-random number in [0, 1), unique-ish per bulb. */
  readonly seed: number;
}

/** Clamps `value` to [0, 1]; non-finite values become 0. */
export function clamp01(value: number): number {
  if (!Number.isFinite(value)) {
    return 0;
  }
  return Math.min(1, Math.max(0, value));
}

/** Fractional part in [0, 1), also for negative values. */
export function fract(value: number): number {
  return value - Math.floor(value);
}

/** Deterministic hash of two integers to [0, 1). */
export function hashSeed(a: number, b: number): number {
  return fract(Math.sin(a * 127.1 + b * 311.7) * 43758.5453);
}

/**
 * Builds the descriptor of a bulb at rest-pose world position (`x`, `y`, `z`, Y-up),
 * the `index`-th of `count` bulbs on string `group`.
 */
export function describeBulb(
  x: number,
  y: number,
  z: number,
  index: number,
  count: number,
  group: number,
  kind: BulbGroupKind,
  colour: BulbColour,
): Bulb {
  return {
    angle: fract(Math.atan2(z, x) / (2 * Math.PI)),
    distance: Math.hypot(x, z),
    height: y,
    lateral: clamp01(Math.abs(x) / SCENE_RADIUS),
    order: count > 1 ? index / (count - 1) : 0,
    index,
    group,
    kind,
    colour,
    seed: hashSeed(group + 1, index + 1),
  };
}
