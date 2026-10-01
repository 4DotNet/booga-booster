import * as THREE from 'three';

import { QueueGuest } from '../../queue/models/queue.models';
import { Gondola, LastOffload, SEATS_PER_GONDOLA } from '../models/ride.models';
import {
  ENTRY_BOOTH,
  EXIT_BOOTH,
  Figure,
  Puddle,
  QUEUE_CAPACITY,
  Vec2,
  advanceFigures,
  agePuddles,
  assignQueueTargets,
  figureId,
  gondolaSceneIndex,
  headColor,
  offloadReaction,
  overflowCount,
  puddleScale,
  queueSlotPosition,
  seatOffset,
  shirtColor,
  spawnExitWalkers,
  spawnPuddles,
} from './park-scene';

/** Instance capacity for figures: 60 queued + 32 seated + 32 exiting + 36 walking into the entry. */
const FIGURE_CAPACITY = 160;

/** Instance capacity for puddles. */
const PUDDLE_CAPACITY = 32;

/** Body capsule: radius and straight length, so the figure is 0.66 m tall. */
const BODY_RADIUS = 0.16;
const BODY_LENGTH = 0.34;
const BODY_CENTER_Y = BODY_LENGTH / 2 + BODY_RADIUS;

/** Head sphere radius and its height above the body centre. */
const HEAD_RADIUS = 0.14;
const HEAD_OFFSET_Y = BODY_CENTER_Y + BODY_RADIUS * 0.6 + HEAD_RADIUS * 0.4;

/** What the park scene needs from the telemetry and queue each frame. */
export interface ParkSceneState {
  readonly queue: readonly QueueGuest[];
  readonly gondolas: readonly Gondola[];
  readonly lastOffload: LastOffload | null;
}

/** Builds a canvas texture of alternating-colour vertical stripes (the booth roof). */
function stripedTexture(a: string, b: string, stripes = 8): THREE.CanvasTexture {
  const canvas = document.createElement('canvas');
  canvas.width = 128;
  canvas.height = 8;
  const context = canvas.getContext('2d');
  if (context) {
    const width = canvas.width / stripes;
    for (let i = 0; i < stripes; i++) {
      context.fillStyle = i % 2 === 0 ? a : b;
      context.fillRect(i * width, 0, width, canvas.height);
    }
  }
  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  return texture;
}

/** A RollerCoaster-Tycoon style ride booth: box, striped cone roof and a coloured sign. */
function createBooth(position: Vec2, signColor: number): THREE.Group {
  const booth = new THREE.Group();
  booth.position.set(position.x, 0, position.z);
  // Face the rig at the origin.
  booth.rotation.y = Math.atan2(-position.x, -position.z);

  const walls = new THREE.Mesh(
    new THREE.BoxGeometry(1.4, 1.4, 1.2),
    new THREE.MeshStandardMaterial({ color: 0xe8dcc0, roughness: 0.8 }),
  );
  walls.position.y = 0.7;
  walls.castShadow = true;
  walls.receiveShadow = true;
  booth.add(walls);

  const roof = new THREE.Mesh(
    new THREE.ConeGeometry(1.15, 0.8, 16),
    new THREE.MeshStandardMaterial({ map: stripedTexture('#d83a3a', '#f4f4f4'), roughness: 0.6 }),
  );
  roof.position.y = 1.8;
  roof.castShadow = true;
  booth.add(roof);

  const sign = new THREE.Mesh(
    new THREE.BoxGeometry(1.0, 0.3, 0.05),
    new THREE.MeshStandardMaterial({ color: signColor, roughness: 0.5 }),
  );
  sign.position.set(0, 1.15, 0.62);
  booth.add(sign);

  return booth;
}

/**
 * Thin Three.js wiring around the pure logic in `park-scene.ts`: the entry and
 * exit booths, one instanced mesh of bodies and one of heads for every figure
 * (queue, seated, exiting), a third for puddles and the `+N` overflow sprite.
 * It owns no policy; geometries, materials and textures are released by the
 * visualization's `disposeScene`, which walks the scene graph.
 */
export class ParkSceneView {
  private readonly bodies: THREE.InstancedMesh;
  private readonly heads: THREE.InstancedMesh;
  private readonly puddleMesh: THREE.InstancedMesh;
  private readonly overflowSprite: THREE.Sprite;
  private readonly overflowCanvas = document.createElement('canvas');
  private readonly overflowTexture: THREE.CanvasTexture;

  private readonly matrix = new THREE.Matrix4();
  private readonly offsetMatrix = new THREE.Matrix4();
  private readonly color = new THREE.Color();

  private queueFigures: Figure[] = [];
  private exitFigures: Figure[] = [];
  private puddles: Puddle[] = [];
  private lastQueue: readonly QueueGuest[] | null = null;
  private seenOffload: number | null = null;
  private drawnOverflow = -1;

  constructor(
    scene: THREE.Scene,
    private readonly reducedMotion: boolean,
  ) {
    scene.add(createBooth(ENTRY_BOOTH, 0x2e9e4f));
    scene.add(createBooth(EXIT_BOOTH, 0xd63b3b));

    const figureMaterial = () => new THREE.MeshStandardMaterial({ roughness: 0.7 });
    this.bodies = this.instanced(
      new THREE.CapsuleGeometry(BODY_RADIUS, BODY_LENGTH, 4, 8),
      figureMaterial(),
      FIGURE_CAPACITY,
    );
    this.heads = this.instanced(
      new THREE.SphereGeometry(HEAD_RADIUS, 12, 10),
      figureMaterial(),
      FIGURE_CAPACITY,
    );
    this.bodies.castShadow = true;
    this.heads.castShadow = true;

    const puddleGeometry = new THREE.CircleGeometry(0.5, 20);
    puddleGeometry.rotateX(-Math.PI / 2);
    this.puddleMesh = this.instanced(
      puddleGeometry,
      new THREE.MeshStandardMaterial({
        color: 0x6bbf3a,
        roughness: 0.3,
        polygonOffset: true,
        polygonOffsetFactor: -2,
      }),
      PUDDLE_CAPACITY,
    );

    this.overflowCanvas.width = 128;
    this.overflowCanvas.height = 64;
    this.overflowTexture = new THREE.CanvasTexture(this.overflowCanvas);
    this.overflowTexture.colorSpace = THREE.SRGBColorSpace;
    this.overflowSprite = new THREE.Sprite(
      new THREE.SpriteMaterial({ map: this.overflowTexture, transparent: true }),
    );
    const end = queueSlotPosition(QUEUE_CAPACITY - 1);
    this.overflowSprite.position.set(end.x + 1.2, 1.2, end.z);
    this.overflowSprite.scale.set(1.6, 0.8, 1);
    this.overflowSprite.visible = false;
    scene.add(this.overflowSprite);

    for (const mesh of [this.bodies, this.heads, this.puddleMesh]) {
      scene.add(mesh);
    }
  }

  /** Advances the scene by `dt` seconds and rewrites every instance. */
  update(dt: number, state: ParkSceneState, podNodes: readonly THREE.Object3D[]): void {
    this.syncQueue(state.queue);
    this.syncOffload(state.lastOffload);

    this.queueFigures = advanceFigures(this.queueFigures, dt);
    this.exitFigures = advanceFigures(this.exitFigures, dt);
    this.puddles = agePuddles(this.puddles, dt);

    let count = 0;
    for (const figure of [...this.queueFigures, ...this.exitFigures]) {
      this.matrix.makeTranslation(figure.position.x, BODY_CENTER_Y, figure.position.z);
      count = this.writeFigure(count, this.matrix, figure.guestNumber, figure);
    }
    count = this.writeSeated(count, state.gondolas, podNodes);

    this.bodies.count = count;
    this.heads.count = count;
    this.flush(this.bodies);
    this.flush(this.heads);
    this.writePuddles();
  }

  private syncQueue(queue: readonly QueueGuest[]): void {
    if (queue !== this.lastQueue) {
      this.lastQueue = queue;
      this.queueFigures = assignQueueTargets(this.queueFigures, queue, this.reducedMotion);
      this.drawOverflow(overflowCount(queue.length));
    }
  }

  private syncOffload(offload: LastOffload | null): void {
    if (offload === null) {
      return;
    }
    const reaction = offloadReaction(this.seenOffload, offload.counter);
    this.seenOffload = reaction.seen;
    if (reaction.spawn) {
      this.exitFigures = [
        ...this.exitFigures,
        ...spawnExitWalkers(offload.riders, this.reducedMotion),
      ];
      this.puddles = [...this.puddles, ...spawnPuddles(offload.riders)].slice(-PUDDLE_CAPACITY);
    }
  }

  /** Seated riders ride along: pod `matrixWorld` times the seat offset. */
  private writeSeated(
    start: number,
    gondolas: readonly Gondola[],
    podNodes: readonly THREE.Object3D[],
  ): number {
    let count = start;
    for (const gondola of gondolas) {
      const node = podNodes[gondolaSceneIndex(gondola.id)];
      if (!node) {
        continue;
      }
      gondola.seats.forEach((seat, seatIndex) => {
        if (!seat.rider || count >= FIGURE_CAPACITY) {
          return;
        }
        const offset = seatOffset(seatIndex);
        this.offsetMatrix.makeTranslation(offset.x, offset.y, offset.z);
        this.matrix.multiplyMatrices(node.matrixWorld, this.offsetMatrix);
        const id = figureId(seat.rider.guestNumber, gondola.id * SEATS_PER_GONDOLA + seatIndex);
        count = this.writeFigure(count, this.matrix, id, seat.rider);
      });
    }
    return count;
  }

  /** Writes one figure (body at `base`, head above it) and returns the next free index. */
  private writeFigure(
    index: number,
    base: THREE.Matrix4,
    guestNumber: number,
    mood: { happiness: number; nausea: number },
  ): number {
    if (index >= FIGURE_CAPACITY) {
      return index;
    }
    this.bodies.setMatrixAt(index, base);
    this.bodies.setColorAt(index, this.color.setHex(shirtColor(guestNumber)));

    this.offsetMatrix.makeTranslation(0, HEAD_OFFSET_Y - BODY_CENTER_Y, 0);
    this.heads.setMatrixAt(index, this.offsetMatrix.premultiply(base));
    this.heads.setColorAt(index, this.color.setHex(headColor(mood)));
    return index + 1;
  }

  private writePuddles(): void {
    this.puddles.forEach((puddle, index) => {
      const scale = puddleScale(puddle.age);
      this.matrix.makeScale(scale, 1, scale);
      this.matrix.setPosition(puddle.position.x, 0.02, puddle.position.z);
      this.puddleMesh.setMatrixAt(index, this.matrix);
    });
    this.puddleMesh.count = this.puddles.length;
    this.puddleMesh.instanceMatrix.needsUpdate = true;
  }

  /** Redraws the `+N` label; called only when the overflow count changes. */
  private drawOverflow(overflow: number): void {
    if (overflow === this.drawnOverflow) {
      return;
    }
    this.drawnOverflow = overflow;
    this.overflowSprite.visible = overflow > 0;
    const context = this.overflowCanvas.getContext('2d');
    if (!context || overflow === 0) {
      return;
    }
    context.clearRect(0, 0, this.overflowCanvas.width, this.overflowCanvas.height);
    context.fillStyle = 'rgba(15, 20, 32, 0.85)';
    context.fillRect(0, 0, this.overflowCanvas.width, this.overflowCanvas.height);
    context.fillStyle = '#ffffff';
    context.font = 'bold 40px sans-serif';
    context.textAlign = 'center';
    context.textBaseline = 'middle';
    context.fillText(`+${overflow}`, this.overflowCanvas.width / 2, this.overflowCanvas.height / 2);
    this.overflowTexture.needsUpdate = true;
  }

  private flush(mesh: THREE.InstancedMesh): void {
    mesh.instanceMatrix.needsUpdate = true;
    if (mesh.instanceColor) {
      mesh.instanceColor.needsUpdate = true;
    }
  }

  private instanced(
    geometry: THREE.BufferGeometry,
    material: THREE.Material,
    capacity: number,
  ): THREE.InstancedMesh {
    const mesh = new THREE.InstancedMesh(geometry, material, capacity);
    mesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
    mesh.frustumCulled = false;
    mesh.count = 0;
    return mesh;
  }
}
