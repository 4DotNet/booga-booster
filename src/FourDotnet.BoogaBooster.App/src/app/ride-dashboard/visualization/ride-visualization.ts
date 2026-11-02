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
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { EffectComposer } from 'three/examples/jsm/postprocessing/EffectComposer.js';
import { OutputPass } from 'three/examples/jsm/postprocessing/OutputPass.js';
import { RenderPass } from 'three/examples/jsm/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/examples/jsm/postprocessing/UnrealBloomPass.js';

import { MotorDirection } from '../models/ride.models';
import { STEADY_BRIGHTNESS, evaluateShow } from './light-show/light-show';
import { spotBrightness } from './light-show/spot-effects';
import {
  RIDE_MODEL_URL,
  RideModel,
  applyBulbs,
  applySpot,
  prepareRideModel,
} from './scenery/ride-model';

/** Night-sky colour behind the stage. */
const NIGHT = 0x05061a;

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
 * Signed angular velocity (rad/s) from a motor's sensed speed. The telemetry
 * `rpm` is already signed — negative while the body physically turns in reverse
 * — so the rotation direction is carried by the sign of `rpm` alone and the
 * commanded direction must NOT be applied again (doing so would cancel out a
 * reversal). A non-finite `rpm` (e.g. an absent/malformed telemetry frame) is
 * treated as stopped rather than corrupting the loop's accumulated rotation.
 */
export function angularVelocity(rpm: number): number {
  return rpmToRadPerSec(finiteOrZero(rpm));
}

/**
 * Free-running pod angle (radians) for the accumulated combined rotation and a
 * pod's phase offset: a counter-rotation against the combined spin plus a
 * phase-shifted swing. This is the trajectory a pod follows while its brake is
 * released. The render loop applies only this angle's per-frame change, and
 * skips it while the brake is engaged, so a braked pod holds its current
 * rotation instead of snapping back to the hub-arm alignment.
 */
export function podFreeAngle(comboA: number, phase: number): number {
  return -comboA + Math.sin(comboA + phase);
}

/**
 * Central visualization: a live 3D render of the ride as designed in
 * `designs/3d/ride-design.blend` — the low-slung mill with four arms, each carrying a
 * hub that spins four two-seat gondolas, in front of a half-round backdrop with eight
 * light poles. The mill and hubs turn at the sensed speed and direction from the
 * operation controls, and the gondola brake holds or frees the pods. Every bulb runs
 * the light show and the pole spots blink and fade (steady under reduced motion).
 *
 * Everything shown is duplicated in the text panels, so the canvas is decorative
 * (`aria-hidden`). WebGL setup is guarded so the component degrades to an empty,
 * harmless element where WebGL is unavailable (e.g. jsdom under test), and a model
 * that fails to load leaves an empty, lit stage.
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

  constructor() {
    afterNextRender(() => this.initScene());
  }

  private initScene(): void {
    const host = this.hostRef().nativeElement;

    let renderer: THREE.WebGLRenderer;
    try {
      renderer = new THREE.WebGLRenderer({ antialias: true });
    } catch {
      // No WebGL (e.g. jsdom during tests). Leave the element empty and harmless.
      return;
    }

    const width = host.clientWidth || 640;
    const height = host.clientHeight || 480;
    const pixelRatio = typeof devicePixelRatio === 'number' ? Math.min(devicePixelRatio, 2) : 1;
    renderer.setPixelRatio(pixelRatio);
    renderer.setSize(width, height, false);
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.1;
    host.appendChild(renderer.domElement);

    // The canvas is created by three.js, not by the Angular template, so the
    // component's emulated-encapsulation styles never match it. Size it explicitly so
    // it always fills the host and displays correctly on HiDPI screens regardless of
    // the (device-pixel-scaled) drawing-buffer size.
    Object.assign(renderer.domElement.style, {
      display: 'block',
      width: '100%',
      height: '100%',
      borderRadius: '0.5rem',
    });

    const scene = new THREE.Scene();
    scene.background = new THREE.Color(NIGHT);
    scene.fog = new THREE.Fog(NIGHT, 32, 80);

    const camera = new THREE.PerspectiveCamera(45, width / height, 0.1, 200);
    camera.position.set(0, 6.5, 21);

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.target.set(0, 2.8, 0);
    controls.minDistance = 6;
    controls.maxDistance = 40;
    controls.maxPolarAngle = Math.PI / 2 - 0.04;

    scene.add(new THREE.HemisphereLight(0x8a96ff, 0x1a0a20, 0.9));
    const key = new THREE.DirectionalLight(0xfff1e6, 1.1);
    key.position.set(4, 14, 16);
    scene.add(key);

    const composer = new EffectComposer(renderer);
    composer.setPixelRatio(pixelRatio);
    composer.setSize(width, height);
    composer.addPass(new RenderPass(scene, camera));
    const bloom = new UnrealBloomPass(new THREE.Vector2(width / 2, height / 2), 0.7, 0.4, 1.05);
    composer.addPass(bloom);
    composer.addPass(new OutputPass());

    const reducedMotion =
      typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches;

    let destroyed = false;
    let model: RideModel | null = null;
    let brightness = new Float32Array(0);
    let colours = new Uint8Array(0);
    new GLTFLoader()
      .loadAsync(RIDE_MODEL_URL)
      .then((gltf) => {
        if (destroyed) {
          disposeObject(gltf.scene);
          return;
        }
        scene.add(gltf.scene);
        model = prepareRideModel(gltf.scene);
        brightness = new Float32Array(model.bulbs.length);
        colours = new Uint8Array(model.bulbs.length);
      })
      .catch(() => {
        // The view is decorative and mirrored by the text panels: an unreachable or
        // corrupt model leaves the empty, lit stage rather than surfacing an error.
      });

    const clock = new THREE.Clock();
    let showTime = 0;
    let mainA = 0;
    let hubA = 0;
    let comboA = 0;
    const prevFree: number[] = [];

    renderer.setAnimationLoop(() => {
      const rawDt = finiteOrZero(clock.getDelta());
      const dt = Math.min(rawDt, MAX_STEP_S);
      showTime += rawDt;
      if (!reducedMotion) {
        const wm = angularVelocity(this.millSpeedRpm());
        const wh = angularVelocity(this.hubSpeedRpm());
        // `angularVelocity` already guards `rpm`, but the accumulators are
        // re-guarded here too so a bad frame can never leave `rotation.y`
        // non-finite, however it might arise.
        mainA = finiteOrZero(mainA + wm * dt);
        hubA = finiteOrZero(hubA + wh * dt);
        comboA = finiteOrZero(comboA + (wm + wh) * dt);
      }

      if (model) {
        this.driveRig(model, mainA, hubA, comboA, prevFree);
        evaluateShow(model.bulbs, showTime, reducedMotion, brightness, colours);
        applyBulbs(model, brightness, colours);
        model.spots.forEach((spot, k) =>
          applySpot(
            spot,
            reducedMotion ? STEADY_BRIGHTNESS : spotBrightness(k, model!.spots.length, showTime),
          ),
        );
      }

      controls.update();
      composer.render();
    });

    // Resizing is deferred to the next animation frame and skipped when the size
    // hasn't changed. Doing this work synchronously inside the observer callback is
    // what provokes the browser's "ResizeObserver loop completed with undelivered
    // notifications" error — which, under the high-rate telemetry re-renders, floods
    // the global error handler and can destabilize the canvas.
    let lastWidth = width;
    let lastHeight = height;
    let resizeHandle = 0;
    const applyResize = (): void => {
      resizeHandle = 0;
      const w = host.clientWidth || width;
      const h = host.clientHeight || height;
      if (w === lastWidth && h === lastHeight) {
        return;
      }
      lastWidth = w;
      lastHeight = h;
      camera.aspect = w / h;
      camera.updateProjectionMatrix();
      renderer.setSize(w, h, false);
      composer.setSize(w, h);
      bloom.resolution.set(w / 2, h / 2);
    };
    const resize = new ResizeObserver(() => {
      if (resizeHandle === 0) {
        resizeHandle = requestAnimationFrame(applyResize);
      }
    });
    resize.observe(host);

    this.destroyRef.onDestroy(() => {
      destroyed = true;
      if (resizeHandle !== 0) {
        cancelAnimationFrame(resizeHandle);
      }
      resize.disconnect();
      renderer.setAnimationLoop(null);
      controls.dispose();
      bloom.dispose();
      composer.dispose();
      disposeObject(scene);
      renderer.dispose();
      renderer.domElement.remove();
    });
  }

  /** Turns the model's mill, hubs and gondolas exactly as the telemetry dictates. */
  private driveRig(
    model: RideModel,
    mainA: number,
    hubA: number,
    comboA: number,
    prevFree: number[],
  ): void {
    if (model.mainPivot) {
      model.mainPivot.rotation.y = mainA;
    }
    for (const hub of model.hubPivots) {
      hub.rotation.y = hubA;
    }
    const braked = this.gondolaBrakeEngaged();
    model.gondolas.forEach((gondola, k) => {
      // The pods' free trajectory keeps advancing with the combined rotation.
      // Apply only its change since the previous frame, and only while the
      // brake is released — so an engaged brake holds each pod exactly where
      // it is rather than snapping it back to the hub-arm alignment.
      const freeAngle = podFreeAngle(comboA, gondola.phase);
      const delta = freeAngle - (prevFree[k] ?? 0);
      prevFree[k] = freeAngle;
      if (!braked) {
        gondola.obj.rotation.y = finiteOrZero(gondola.obj.rotation.y + delta);
      }
    });
  }
}

/** Disposes every geometry, material and texture under `root`. */
function disposeObject(root: THREE.Object3D): void {
  const materials = new Set<THREE.Material>();
  root.traverse((object) => {
    const mesh = object as Partial<THREE.Mesh>;
    mesh.geometry?.dispose();
    const material = mesh.material;
    (Array.isArray(material) ? material : material ? [material] : []).forEach((m) =>
      materials.add(m),
    );
  });
  for (const material of materials) {
    for (const value of Object.values(material)) {
      if (value instanceof THREE.Texture) {
        value.dispose();
      }
    }
    material.dispose();
  }
}
