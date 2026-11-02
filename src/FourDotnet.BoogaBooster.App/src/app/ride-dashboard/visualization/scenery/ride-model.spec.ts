import * as THREE from 'three';

import {
  applyBulbs,
  applySpot,
  bulbKindOf,
  findRigPivots,
  parseBulbRecords,
  prepareRideModel,
} from './ride-model';

/** A miniature of the exported model: one hub with one gondola, a base and a pole. */
function fakeModel(): THREE.Group {
  const root = new THREE.Group();
  const main = new THREE.Group();
  main.name = 'MainPivot';
  main.position.y = 1;
  root.add(main);

  const arm = new THREE.Group();
  arm.name = 'MainArmLights_1';
  arm.position.x = 3;
  main.add(arm);
  const armBulbs = new THREE.Object3D();
  armBulbs.name = 'MainArmLights_1_Bulbs';
  armBulbs.userData = { bulbs: [0, 0.2, 0, 0.04, 0, 1, 0.2, 0, 0.04, 1, 2, 0.2, 0, 0.04, 2] };
  arm.add(armBulbs);

  const hub = new THREE.Group();
  hub.name = 'HubPivot_1';
  hub.position.x = 6;
  main.add(hub);
  const pod = new THREE.Group();
  pod.name = 'GondolaPivot_1_2';
  hub.add(pod);

  const base = new THREE.Object3D();
  base.name = 'BaseLights_Bulbs';
  base.userData = { bulbs: [1, 0, 0, 0.03, 1, 'bad', 0, 0, 0.03, 1] };
  root.add(base);

  const lamp = new THREE.Group();
  lamp.name = 'PoleLamp_1';
  const lens = new THREE.Mesh(
    new THREE.CircleGeometry(0.2),
    new THREE.MeshStandardMaterial({ emissive: 0xff2e93 }),
  );
  const housing = new THREE.Mesh(new THREE.BoxGeometry(), new THREE.MeshStandardMaterial());
  const spot = new THREE.SpotLight(0xff2e93, 3e6, 0, 0.16);
  spot.name = 'PoleSpot_1';
  lamp.add(housing, lens, spot);
  root.add(lamp);
  return root;
}

describe('ride model', () => {
  it('parses bulb records and skips malformed ones', () => {
    expect(parseBulbRecords([1, 2, 3, 0.1, 2])).toEqual([
      { x: 1, y: 2, z: 3, radius: 0.1, colour: 2 },
    ]);
    expect(parseBulbRecords([1, 2, 3, 0.1, 7, 1, 2, 3])).toEqual([]);
    expect(parseBulbRecords('nope')).toEqual([]);
  });

  it('maps exported node names to bulb string kinds', () => {
    expect(bulbKindOf('HubArmLights_2_3_Bulbs')).toBe('hub-arm');
    expect(bulbKindOf('HubLights_4_Bulbs')).toBe('hub');
    expect(bulbKindOf('Gondola_1_1_Bulbs')).toBe('gondola');
    expect(bulbKindOf('BackdropLights_Bulbs')).toBe('backdrop');
    expect(bulbKindOf('Floor')).toBeNull();
  });

  it('finds the pivots by name and skips missing ones', () => {
    const { mainPivot, hubPivots, gondolas } = findRigPivots(fakeModel());

    expect(mainPivot?.name).toBe('MainPivot');
    expect(hubPivots.map((h) => h.name)).toEqual(['HubPivot_1']);
    expect(gondolas).toHaveLength(1);
    expect(gondolas[0].obj.name).toBe('GondolaPivot_1_2');
    expect(gondolas[0].phase).toBeCloseTo(0.7, 6);
    expect(findRigPivots(new THREE.Group()).mainPivot).toBeNull();
  });

  it('turns bulb records into instanced bulbs described from their world rest pose', () => {
    const model = prepareRideModel(fakeModel());

    expect(model.bulbs).toHaveLength(4);
    expect(model.bulbGroups.map((g) => [g.mesh.name, g.offset, g.count])).toEqual([
      ['BaseLights_Bulbs_Instanced', 0, 1],
      ['MainArmLights_1_Bulbs_Instanced', 1, 3],
    ]);
    const armBulb = model.bulbs[1];
    expect(armBulb.kind).toBe('main-arm');
    expect(armBulb.height).toBeCloseTo(1.2, 6);
    expect(armBulb.distance).toBeCloseTo(3, 6);
    expect(model.bulbs[3].order).toBe(1);
    expect(model.bulbGroups[1].mesh.parent?.name).toBe('MainArmLights_1_Bulbs');
  });

  it('takes over the pole spots with their own lens and a beam', () => {
    const root = fakeModel();
    const sharedLens = (root.getObjectByName('PoleLamp_1')!.children[1] as THREE.Mesh).material;
    const [spot] = prepareRideModel(root).spots;

    expect(spot.light.name).toBe('PoleSpot_1');
    expect(spot.lens).not.toBe(sharedLens);
    expect(spot.beam.parent).toBe(spot.light);

    applySpot(spot, 0.5);
    expect(spot.light.intensity).toBeGreaterThan(0);
    expect(spot.lens!.emissiveIntensity).toBeCloseTo(3.3, 6);
    applySpot(spot, 0);
    expect(spot.light.intensity).toBe(0);
    expect(spot.beam.material.uniforms['uOpacity'].value).toBe(0);
  });

  it('writes brighter colours for brighter bulbs', () => {
    const model = prepareRideModel(fakeModel());
    const colour = new THREE.Color();
    const bright = new Float32Array([1, 1, 1, 1]);
    const dark = new Float32Array([0, 0, 0, 0]);
    const colours = new Uint8Array([1, 0, 1, 2]);

    applyBulbs(model, bright, colours);
    model.bulbGroups[0].mesh.getColorAt(0, colour);
    const lit = colour.r;
    applyBulbs(model, dark, colours);
    model.bulbGroups[0].mesh.getColorAt(0, colour);

    expect(lit).toBeGreaterThan(1);
    expect(colour.r).toBeLessThan(0.1);
  });
});
