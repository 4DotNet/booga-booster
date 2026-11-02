import * as THREE from 'three';

import { Bulb, BulbColour, BulbGroupKind, describeBulb } from '../light-show/bulb';

/** URL of the web model exported from `designs/3d/ride-design.blend`. */
export const RIDE_MODEL_URL = '3dmodels/booga-booster.glb';

/** Linear-space bulb colours for warm white, pink and cyan (index = {@link BulbColour}). */
const BULB_RGB: readonly THREE.Color[] = [
  new THREE.Color('#ffd27a'),
  new THREE.Color('#ff2e93'),
  new THREE.Color('#36cfff'),
];

/** Glow multiplier at full brightness: above 1 so lit bulbs feed the bloom pass. */
const BULB_GLOW = 3.2;

/** Faint level an "off" bulb still shows, so it reads as glass rather than a hole. */
const BULB_OFF = 0.05;

/** Peak spotlight intensity, in candela. */
const SPOT_MAX_CANDELA = 900;

/** Length of the visible beam cone, in metres. */
const BEAM_LENGTH = 15;

/** Opacity of the beam cone at full brightness. */
const BEAM_OPACITY = 0.16;

/** Node-name prefixes of each bulb string kind, as exported from the design. */
const KIND_BY_PREFIX: readonly (readonly [string, BulbGroupKind])[] = [
  ['MainArmLights_', 'main-arm'],
  ['HubArmLights_', 'hub-arm'],
  ['HubLights_', 'hub'],
  ['MainCrownLights', 'crown'],
  ['BaseLights', 'base'],
  ['Gondola_', 'gondola'],
  ['BackdropLights', 'backdrop'],
  ['PoleBulbs_', 'pole'],
];

/** One record in a node's `userData.bulbs`: position (node-local, Y-up), radius, colour. */
export interface BulbRecord {
  readonly x: number;
  readonly y: number;
  readonly z: number;
  readonly radius: number;
  readonly colour: BulbColour;
}

/** A string of bulbs drawn as one instanced mesh. */
export interface BulbGroup {
  readonly mesh: THREE.InstancedMesh;
  /** Offset of this group's first bulb in {@link RideModel.bulbs}. */
  readonly offset: number;
  readonly count: number;
}

/** A gondola pivot and its phase offset in the free-running trajectory. */
export interface GondolaPivot {
  readonly obj: THREE.Object3D;
  readonly phase: number;
}

/** A pole spotlight with its glowing lens and visible beam. */
export interface PoleSpot {
  readonly light: THREE.SpotLight;
  readonly lens: THREE.MeshStandardMaterial | null;
  readonly beam: THREE.Mesh<THREE.BufferGeometry, THREE.ShaderMaterial>;
}

/** Everything the visualization drives on the loaded model. */
export interface RideModel {
  readonly mainPivot: THREE.Object3D | null;
  readonly hubPivots: readonly THREE.Object3D[];
  readonly gondolas: readonly GondolaPivot[];
  readonly bulbs: readonly Bulb[];
  readonly bulbGroups: readonly BulbGroup[];
  readonly spots: readonly PoleSpot[];
}

/**
 * Parses a node's `userData.bulbs` (flat `[x, y, z, radius, colour, …]`). Anything
 * malformed is skipped rather than thrown, so a bad export degrades to fewer bulbs.
 */
export function parseBulbRecords(value: unknown): BulbRecord[] {
  if (!Array.isArray(value)) {
    return [];
  }
  const records: BulbRecord[] = [];
  for (let i = 0; i + 4 < value.length; i += 5) {
    const [x, y, z, radius, colour] = value.slice(i, i + 5) as unknown[];
    if (
      [x, y, z, radius].every((n) => typeof n === 'number' && Number.isFinite(n)) &&
      (colour === 0 || colour === 1 || colour === 2)
    ) {
      records.push({
        x: x as number,
        y: y as number,
        z: z as number,
        radius: radius as number,
        colour,
      });
    }
  }
  return records;
}

/** The bulb string kind of an exported `<object>_Bulbs` node, or `null`. */
export function bulbKindOf(nodeName: string): BulbGroupKind | null {
  return KIND_BY_PREFIX.find(([prefix]) => nodeName.startsWith(prefix))?.[1] ?? null;
}

/** Finds the rig's pivots by their design names; missing ones are simply absent. */
export function findRigPivots(
  root: THREE.Object3D,
): Pick<RideModel, 'mainPivot' | 'hubPivots' | 'gondolas'> {
  const mainPivot = root.getObjectByName('MainPivot') ?? null;
  const hubPivots: THREE.Object3D[] = [];
  const gondolas: GondolaPivot[] = [];
  for (let i = 1; i <= 4; i++) {
    const hub = root.getObjectByName(`HubPivot_${i}`);
    if (hub) {
      hubPivots.push(hub);
    }
    for (let j = 1; j <= 4; j++) {
      const pod = root.getObjectByName(`GondolaPivot_${i}_${j}`);
      if (pod) {
        // Same phase spread the hand-built rig used: (k * 4 + j) * 0.7, zero-based.
        gondolas.push({ obj: pod, phase: ((i - 1) * 4 + (j - 1)) * 0.7 });
      }
    }
  }
  return { mainPivot, hubPivots, gondolas };
}

/**
 * Replaces every exported bulb record with instanced spheres parented to the node
 * that carried it, and describes each bulb from its rest-pose world position. Call it
 * while the rig is at rest (straight after loading).
 */
export function buildBulbGroups(root: THREE.Object3D): Pick<RideModel, 'bulbs' | 'bulbGroups'> {
  root.updateMatrixWorld(true);
  const carriers: { node: THREE.Object3D; kind: BulbGroupKind; records: BulbRecord[] }[] = [];
  root.traverse((node) => {
    const kind = bulbKindOf(node.name);
    const records = parseBulbRecords(node.userData['bulbs']);
    if (kind && records.length > 0) {
      carriers.push({ node, kind, records });
    }
  });
  carriers.sort((a, b) => a.node.name.localeCompare(b.node.name));

  const geometry = new THREE.SphereGeometry(1, 10, 8);
  const material = new THREE.MeshBasicMaterial({ toneMapped: false });
  const bulbs: Bulb[] = [];
  const bulbGroups: BulbGroup[] = [];
  const matrix = new THREE.Matrix4();
  const world = new THREE.Vector3();

  carriers.forEach(({ node, kind, records }, group) => {
    const mesh = new THREE.InstancedMesh(geometry, material, records.length);
    mesh.name = `${node.name}_Instanced`;
    mesh.frustumCulled = false;
    const offset = bulbs.length;
    records.forEach((r, index) => {
      matrix.makeScale(r.radius, r.radius, r.radius).setPosition(r.x, r.y, r.z);
      mesh.setMatrixAt(index, matrix);
      mesh.setColorAt(index, BULB_RGB[r.colour]);
      world.set(r.x, r.y, r.z).applyMatrix4(node.matrixWorld);
      bulbs.push(
        describeBulb(world.x, world.y, world.z, index, records.length, group, kind, r.colour),
      );
    });
    node.add(mesh);
    bulbGroups.push({ mesh, offset, count: records.length });
  });
  return { bulbs, bulbGroups };
}

/**
 * Takes over the exported pole spotlights: one per `PoleLamp_<k>` head, with the
 * lens material cloned per pole (so each can glow on its own) and an additive beam
 * cone that makes the narrow beam visible.
 */
export function preparePoleSpots(root: THREE.Object3D): PoleSpot[] {
  const spots: PoleSpot[] = [];
  for (let k = 1; ; k++) {
    const head = root.getObjectByName(`PoleLamp_${k}`);
    if (!head) {
      break;
    }
    const parts: THREE.Object3D[] = [];
    head.traverse((child) => parts.push(child));
    const spot = parts.find((c): c is THREE.SpotLight => (c as THREE.SpotLight).isSpotLight);
    if (!spot) {
      continue;
    }
    const lensMesh = parts.find(
      (c): c is THREE.Mesh<THREE.BufferGeometry, THREE.MeshStandardMaterial> =>
        (c as THREE.Mesh).isMesh && isGlowing((c as THREE.Mesh).material),
    );
    let lens: THREE.MeshStandardMaterial | null = null;
    if (lensMesh) {
      lens = lensMesh.material.clone();
      lensMesh.material = lens;
    }
    spot.intensity = SPOT_MAX_CANDELA;
    spot.decay = 2;
    spot.distance = 0;
    spot.penumbra = 0.35;
    spot.castShadow = false;
    const beam = beamCone(spot.angle, spot.color);
    spot.add(beam);
    spots.push({ light: spot, lens, beam });
  }
  return spots;
}

function isGlowing(material: THREE.Material | THREE.Material[]): boolean {
  const m = material as THREE.MeshStandardMaterial;
  return !Array.isArray(material) && m.emissive instanceof THREE.Color && m.emissive.getHex() !== 0;
}

function beamCone(
  angle: number,
  color: THREE.Color,
): THREE.Mesh<THREE.BufferGeometry, THREE.ShaderMaterial> {
  const radius = Math.tan(angle) * BEAM_LENGTH;
  const geometry = new THREE.ConeGeometry(radius, BEAM_LENGTH, 32, 1, true);
  // Tip at the lamp, opening along the light's local -Z.
  geometry.translate(0, -BEAM_LENGTH / 2, 0);
  geometry.rotateX(Math.PI / 2);
  const material = new THREE.ShaderMaterial({
    uniforms: {
      uColor: { value: color.clone() },
      uOpacity: { value: BEAM_OPACITY },
      uLength: { value: BEAM_LENGTH },
    },
    vertexShader: `
      varying float vAlong;
      uniform float uLength;
      void main() {
        vAlong = clamp(-position.z / uLength, 0.0, 1.0);
        gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
      }`,
    fragmentShader: `
      varying float vAlong;
      uniform vec3 uColor;
      uniform float uOpacity;
      void main() {
        float fade = (1.0 - vAlong) * (1.0 - vAlong);
        gl_FragColor = vec4(uColor, uOpacity * fade);
      }`,
    transparent: true,
    depthWrite: false,
    blending: THREE.AdditiveBlending,
    side: THREE.DoubleSide,
  });
  const beam = new THREE.Mesh(geometry, material);
  beam.name = 'PoleBeam';
  beam.frustumCulled = false;
  return beam;
}

/** Prepares a freshly loaded model for animation. */
export function prepareRideModel(root: THREE.Object3D): RideModel {
  return { ...findRigPivots(root), ...buildBulbGroups(root), spots: preparePoleSpots(root) };
}

const scratch = new THREE.Color();

/** Writes the show's brightness and colour for every bulb into the instanced meshes. */
export function applyBulbs(model: RideModel, brightness: Float32Array, colours: Uint8Array): void {
  for (const group of model.bulbGroups) {
    for (let i = 0; i < group.count; i++) {
      const k = group.offset + i;
      const level = BULB_OFF + (BULB_GLOW - BULB_OFF) * brightness[k];
      scratch.copy(BULB_RGB[colours[k]] ?? BULB_RGB[0]).multiplyScalar(level);
      group.mesh.setColorAt(i, scratch);
    }
    if (group.mesh.instanceColor) {
      group.mesh.instanceColor.needsUpdate = true;
    }
  }
}

/** Sets a pole spot's light, lens glow and beam to `level` ∈ [0, 1]. */
export function applySpot(spot: PoleSpot, level: number): void {
  spot.light.intensity = SPOT_MAX_CANDELA * level;
  if (spot.lens) {
    spot.lens.emissiveIntensity = 0.3 + 6 * level;
  }
  spot.beam.material.uniforms['uOpacity'].value = BEAM_OPACITY * level;
}
