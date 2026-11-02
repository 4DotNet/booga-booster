# Tasks

## 1. Web model export

- [x] 1.1 Add `designs/3d/export_web_model.py` implementing D1 (rest pose, bulb extraction into `extras.bulbs`, gondola rim bulbs on `GondolaBulbs_<i>_<j>`, theme colours on spot heads, 8192×2048 backdrop swap, GLB export of the ride, backdrop, floor and poles only). Verify by running it in Blender and checking the console report lists every bulb group with a non-zero count.
- [x] 1.2 Export `src/FourDotnet.BoogaBooster.App/public/3dmodels/booga-booster.glb` and remove the unused `boogabooster.fbx`. Verify the GLB loads in Blender's glTF importer with the expected node names and that `grep` finds no reference to the `.fbx`.

## 2. Light-show engine (plain TypeScript)

- [x] 2.1 Add `visualization/light-show/bulb.ts` (bulb descriptor, colours, group kinds) and `light-patterns.ts` with the 12 patterns of D3. Verify with a Vitest spec: 12 unique names, positive loop durations, brightness in [0, 1] and deterministic for sampled bulbs and phases.
- [x] 2.2 Add `light-show-sequencer.ts` (D4): 5 loops per pattern, cycling forever, starting at pattern 0. Verify with a spec covering start, the switch after the fifth loop, and the wrap after the twelfth pattern.
- [x] 2.3 Add `spot-effects.ts` (D5): per-pole fade + blink brightness with phase offsets. Verify with a spec that values stay in [0, 1], vary over time, and differ between poles.
- [x] 2.4 Add the flash-rate safety spec (D7) over every pattern and spot effect. Verify it passes with at most 3 off→on transitions per second.

## 3. Scenery in the visualization

- [ ] 3.1 Add `scenery/ride-model.ts`: load the GLB, find the pivots by name, build the `InstancedMesh` bulb groups and bulb descriptors from `extras.bulbs`, and collect the pole spots, lenses and beam cones. Verify with a spec that feeds a hand-built `Object3D` tree with `userData.bulbs` and asserts the instanced groups, descriptors and pivot lookup (missing names skipped).
- [ ] 3.2 Rework `ride-visualization.ts` to show the loaded model (night lighting, new camera framing, bloom per D6), drive the found pivots with the existing telemetry maths, and run the light show and spot effects (steady glow under reduced motion). Verify the existing `ride-visualization.spec.ts` still passes and add a case that the component mounts without WebGL and requests no model.
- [ ] 3.3 Run `npm run build` and `npm test` in the Angular app. Verify both pass.

## 4. Integration

- [ ] 4.1 Run the app (`npm start`), open the dashboard and check that the model, backdrop, poles, bulb patterns (switching after 5 loops) and blinking/fading spots render, and that rotation still follows the operation controls. Record a screenshot in the PR.
