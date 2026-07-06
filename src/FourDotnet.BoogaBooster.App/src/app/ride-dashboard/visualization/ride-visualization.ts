import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  inject,
  input,
  viewChild,
} from '@angular/core';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';

import { MotorDirection, podMotionFor } from '../models/ride.models';

/** Ride geometry (metres) — mirrors the reference model's parameter set. */
const P = {
  towerRadius: 0.6,
  towerHeight: 6.0,
  baseSize: 3.4,
  baseHeight: 0.35,
  mainMotorR: 1.05,
  mainMotorH: 1.3,
  mainArmLen: 6.0,
  mainArmW: 0.42,
  mainArmH: 0.42,
  hubR: 0.62,
  hubH: 1.0,
  hubArmLen: 2.0,
  hubArmW: 0.26,
  hubArmH: 0.26,
  gondolaLen: 2.0,
  gondolaW: 0.85,
  gondolaH: 1.15,
  gondolaDrop: 0.55,
};

/** Height of the arm plane above the ground. */
const ARM_HEIGHT = P.towerHeight;

/** Largest simulation step applied per frame, in seconds (guards tab-switch jumps). */
const MAX_STEP_S = 0.05;

/** Converts revolutions per minute to radians per second. */
export function rpmToRadPerSec(rpm: number): number {
  return (rpm / 60) * Math.PI * 2;
}

/**
 * Coerces a non-finite number (`NaN`/`±Infinity`) to `0`; otherwise returns it
 * unchanged. A missing or corrupt telemetry frame must never inject a
 * non-finite value into the scene graph — that silently blanks the whole
 * rendered rig, since Three.js matrices built from `NaN` propagate to every
 * descendant transform.
 */
function finiteOrZero(value: number): number {
  return Number.isFinite(value) ? value : 0;
}

/**
 * Signed angular velocity (rad/s) for a motor's sensed speed and direction.
 * A non-finite `rpm` (e.g. an absent/malformed telemetry frame) is treated as
 * stopped rather than corrupting the animation loop's accumulated rotation.
 */
export function angularVelocity(rpm: number, direction: MotorDirection): number {
  return rpmToRadPerSec(finiteOrZero(rpm)) * (direction === 'reverse' ? -1 : 1);
}

interface GondolaNode {
  readonly obj: THREE.Object3D;
  readonly phase: number;
}

/**
 * Central visualization: a live 3D render of the ride — one central mill turning
 * four arms, each carrying a hub that spins four gondolas. The mill and hubs turn
 * at the sensed speed and direction from the operation controls (both can run
 * either way), and the gondola brake holds or frees the pods.
 *
 * Everything shown is duplicated in the text panels, so the canvas is decorative
 * (`aria-hidden`). WebGL setup is guarded so the component degrades to an empty,
 * harmless element where WebGL is unavailable (e.g. jsdom under test).
 */
@Component({
  selector: 'bb-ride-visualization',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { 'aria-hidden': 'true' },
  template: `<div #host class="viz"></div>`,
  styles: `
    :host {
      display: block;
      width: 100%;
      height: 100%;
    }
    .viz {
      width: 100%;
      height: 100%;
    }
    .viz canvas {
      display: block;
      width: 100%;
      height: 100%;
      border-radius: 0.5rem;
    }
  `,
})
export class RideVisualization {
  readonly millSpeedRpm = input.required<number>();
  readonly millDirection = input.required<MotorDirection>();
  readonly hubSpeedRpm = input.required<number>();
  readonly hubDirection = input.required<MotorDirection>();
  readonly gondolaBrakeEngaged = input.required<boolean>();

  private readonly hostRef = viewChild.required<ElementRef<HTMLDivElement>>('host');
  private readonly destroyRef = inject(DestroyRef);

  private renderer?: THREE.WebGLRenderer;

  constructor() {
    afterNextRender(() => this.initScene());
  }

  private initScene(): void {
    const host = this.hostRef().nativeElement;

    let renderer: THREE.WebGLRenderer;
    try {
      renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
    } catch {
      // No WebGL (e.g. jsdom during tests). Leave the element empty and harmless.
      return;
    }
    this.renderer = renderer;

    const width = host.clientWidth || 640;
    const height = host.clientHeight || 480;
    const pixelRatio = typeof devicePixelRatio === 'number' ? Math.min(devicePixelRatio, 2) : 1;
    renderer.setPixelRatio(pixelRatio);
    renderer.setSize(width, height, false);
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    host.appendChild(renderer.domElement);

    const scene = new THREE.Scene();
    scene.fog = new THREE.Fog(0x0f1420, 40, 95);

    const camera = new THREE.PerspectiveCamera(48, width / height, 0.1, 1000);
    camera.position.set(18, 13, 18);

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.target.set(0, 5.5, 0);

    this.addLighting(scene);
    this.addGround(scene);
    this.addStaticStructure(scene);
    const { mainPivot, hubPivots, gondolas } = this.addRig(scene);

    const reducedMotion =
      typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches;

    const clock = new THREE.Clock();
    let mainA = 0;
    let hubA = 0;
    let comboA = 0;

    renderer.setAnimationLoop(() => {
      const dt = finiteOrZero(Math.min(clock.getDelta(), MAX_STEP_S));
      if (!reducedMotion) {
        const wm = angularVelocity(this.millSpeedRpm(), this.millDirection());
        const wh = angularVelocity(this.hubSpeedRpm(), this.hubDirection());
        // `angularVelocity` already guards `rpm`, but the accumulators are
        // re-guarded here too so a bad frame can never leave `rotation.y`
        // non-finite, however it might arise.
        mainA = finiteOrZero(mainA + wm * dt);
        hubA = finiteOrZero(hubA + wh * dt);
        comboA = finiteOrZero(comboA + (wm + wh) * dt);
      }

      const pod = podMotionFor(this.gondolaBrakeEngaged());
      mainPivot.rotation.y = mainA;
      for (const hub of hubPivots) {
        hub.rotation.y = hubA;
      }
      for (const gondola of gondolas) {
        gondola.obj.rotation.y =
          -pod.freedom * comboA + pod.swing * Math.sin(comboA + gondola.phase);
      }

      controls.update();
      renderer.render(scene, camera);
    });

    const resize = new ResizeObserver(() => {
      const w = host.clientWidth || width;
      const h = host.clientHeight || height;
      camera.aspect = w / h;
      camera.updateProjectionMatrix();
      renderer.setSize(w, h, false);
    });
    resize.observe(host);

    this.destroyRef.onDestroy(() => {
      resize.disconnect();
      renderer.setAnimationLoop(null);
      controls.dispose();
      this.disposeScene(scene);
      renderer.dispose();
      renderer.domElement.remove();
      this.renderer = undefined;
    });
  }

  private addLighting(scene: THREE.Scene): void {
    scene.add(new THREE.HemisphereLight(0xbfd4ff, 0x2a2620, 1.0));
    const sun = new THREE.DirectionalLight(0xffffff, 2.2);
    sun.position.set(14, 24, 10);
    sun.castShadow = true;
    sun.shadow.mapSize.set(2048, 2048);
    Object.assign(sun.shadow.camera, {
      left: -20,
      right: 20,
      top: 20,
      bottom: -20,
      near: 1,
      far: 70,
    });
    scene.add(sun);
  }

  private addGround(scene: THREE.Scene): void {
    const ground = new THREE.Mesh(
      new THREE.CircleGeometry(50, 72),
      new THREE.MeshStandardMaterial({ color: 0x232c3d, roughness: 1 }),
    );
    ground.rotation.x = -Math.PI / 2;
    ground.receiveShadow = true;
    scene.add(ground);
  }

  private addStaticStructure(scene: THREE.Scene): void {
    const mats = this.materials();
    const base = this.box(P.baseSize, P.baseHeight, P.baseSize, mats.base);
    base.position.y = P.baseHeight / 2;
    scene.add(base);

    const tower = this.cyl(P.towerRadius, P.towerHeight, mats.tower);
    tower.position.y = P.towerHeight / 2;
    scene.add(tower);

    const motor = this.cyl(P.mainMotorR, P.mainMotorH, mats.motor);
    motor.position.y = ARM_HEIGHT;
    scene.add(motor);
  }

  private addRig(scene: THREE.Scene): {
    mainPivot: THREE.Group;
    hubPivots: THREE.Group[];
    gondolas: GondolaNode[];
  } {
    const mats = this.materials();
    const mainPivot = new THREE.Group();
    mainPivot.position.y = ARM_HEIGHT;
    scene.add(mainPivot);

    const cap = this.cyl(P.mainMotorR * 0.6, 0.35, mats.tower);
    cap.position.y = P.mainMotorH / 2 + 0.1;
    mainPivot.add(cap);

    const hubPivots: THREE.Group[] = [];
    const gondolas: GondolaNode[] = [];

    for (let k = 0; k < 4; k++) {
      const armHinge = new THREE.Group();
      armHinge.rotation.y = (k * Math.PI) / 2;
      mainPivot.add(armHinge);

      const arm = this.box(P.mainArmLen, P.mainArmH, P.mainArmW, mats.arm);
      arm.position.x = P.mainArmLen / 2;
      armHinge.add(arm);

      const hubPivot = new THREE.Group();
      hubPivot.position.x = P.mainArmLen;
      armHinge.add(hubPivot);
      hubPivots.push(hubPivot);

      hubPivot.add(this.cyl(P.hubR, P.hubH, mats.hub));
      const hubCap = this.cyl(P.hubR * 0.6, 0.3, mats.tower);
      hubCap.position.y = P.hubH / 2 + 0.08;
      hubPivot.add(hubCap);

      for (let j = 0; j < 4; j++) {
        const hubArmHinge = new THREE.Group();
        hubArmHinge.rotation.y = (j * Math.PI) / 2;
        hubPivot.add(hubArmHinge);

        const hubArm = this.box(P.hubArmLen, P.hubArmH, P.hubArmW, mats.hubArm);
        hubArm.position.x = P.hubArmLen / 2;
        hubArmHinge.add(hubArm);

        const gondolaPivot = new THREE.Group();
        gondolaPivot.position.x = P.hubArmLen;
        hubArmHinge.add(gondolaPivot);

        const pod = this.box(P.gondolaLen, P.gondolaH, P.gondolaW, mats.gondola);
        pod.position.y = -P.gondolaDrop;
        gondolaPivot.add(pod);

        gondolas.push({ obj: gondolaPivot, phase: (k * 4 + j) * 0.7 });
      }
    }

    return { mainPivot, hubPivots, gondolas };
  }

  private materials() {
    const make = (color: number) =>
      new THREE.MeshStandardMaterial({ color, roughness: 0.55, metalness: 0.12 });
    return {
      base: make(0x47474d),
      tower: make(0x8d9096),
      motor: make(0xcc2e2e),
      arm: make(0xe6b826),
      hub: make(0x347ad9),
      hubArm: make(0xf2d959),
      gondola: make(0x27b374),
    };
  }

  private box(l: number, h: number, w: number, mat: THREE.Material): THREE.Mesh {
    const mesh = new THREE.Mesh(new THREE.BoxGeometry(l, h, w), mat);
    mesh.castShadow = true;
    mesh.receiveShadow = true;
    return mesh;
  }

  private cyl(r: number, h: number, mat: THREE.Material, segments = 32): THREE.Mesh {
    const mesh = new THREE.Mesh(new THREE.CylinderGeometry(r, r, h, segments), mat);
    mesh.castShadow = true;
    return mesh;
  }

  private disposeScene(scene: THREE.Scene): void {
    scene.traverse((object) => {
      const mesh = object as Partial<THREE.Mesh>;
      mesh.geometry?.dispose();
      const material = mesh.material;
      if (Array.isArray(material)) {
        material.forEach((m) => m.dispose());
      } else {
        material?.dispose();
      }
    });
  }
}
