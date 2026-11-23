# Design

## Context

`ride-visualization.ts` builds the rig from boxes and cylinders in code and animates
three kinds of pivot (mill, hub, gondola) from telemetry inputs. The designed model in
`designs/3d/ride-design.blend` keeps the same pivot hierarchy and names (`MainPivot`,
`HubPivot_<i>`, `GondolaPivot_<i>_<j>`), but its bulbs are merged into a few large
meshes per part (`MainArmLights_<i>`, `HubArmLights_<i>_<j>`, `HubLights_<i>`,
`MainCrownLights`, `BaseLights`, `BackdropLights`, `PoleBulbs_<k>`, plus the bulbs on the
`mesh_gondola` rim) — several thousand bulbs that cannot be addressed one by one. The
poles carry Blender spot lights `PoleSpot_<k>` under `PoleLamp_<k>` heads.

## Goals / Non-Goals

**Goals:**
- One self-describing web model the app loads at runtime.
- Per-bulb control at 60 fps with a handful of draw calls.
- Light-show logic as plain, unit-testable TypeScript, separate from three.js plumbing.

**Non-Goals:**
- Changing the telemetry-driven motion maths (`angularVelocity`, `podFreeAngle` stay).
- Real-time shadows from the pole spots (cost); the existing sun shadow is dropped for
  a night scene.
- Occupancy display on gondolas (unchanged scope).

## Decisions

### D1. Export a GLB with bulb metadata in glTF extras

`designs/3d/export_web_model.py` (run in Blender, headless or through the Blender MCP
server) sets the frame to 0 so every driven pivot is at rest, strips the bulb
spheres out of each light mesh (keeping the chrome sockets), and stores the bulbs on
that node as a custom property `bulbs`: a flat array `[x, y, z, radius, colour, …]` in
glTF (Y-up) node-local coordinates, `colour` ∈ {0 warm, 1 pink, 2 cyan}. The glTF
exporter writes custom properties as `extras`, which three.js exposes as
`userData`. Gondola rim bulbs move to a `GondolaBulbs_<i>_<j>` node under each gondola.
The spot heads get `extras.theme_color` and their `KHR_lights_punctual` spots are
exported. The 16k backdrop JPEG is swapped for an 8192×2048 copy during export
(most GPUs cap textures at 8192 or 16384) and restored after.

*Alternatives:* a side-car JSON (two files to keep in step); `EXT_mesh_gpu_instancing`
via geometry nodes (fragile exporter path); keeping merged bulb meshes and animating a
vertex attribute (needs a per-vertex bulb id and a custom shader).

### D2. One `InstancedMesh` per bulb group

At load the app replaces each `bulbs` node's data with an `InstancedMesh` of a unit
sphere (one per node, parented to that node, so bulbs ride along with the rig). Per
frame it writes `instanceColor = baseColour × brightness × glow`. The material is
`MeshBasicMaterial` with `toneMapped: false`, so values above 1 feed the bloom pass.
About 45 instanced draw calls cover every bulb.

### D3. Patterns are pure functions over a bulb descriptor

```ts
interface Bulb { x: number; y: number; z: number;   // rest-pose world position
                 angle: number; radius: number;      // around the ride centre
                 order: number;                       // 0..1 along its string
                 group: number; groupKind: BulbGroupKind; colour: BulbColour; seed: number }
interface LightPattern { name: string; loopSeconds: number;
                         brightness(b: Bulb, phase: number): number;      // phase ∈ [0,1)
                         colour?(b: Bulb, phase: number): BulbColour }
```

The 12 patterns: *chase, alternate, radial-wave, sweep, rise, twinkle, breathe, comet,
group-cascade, heartbeat, split, colour-cycle*. Descriptors are computed once from the
rest pose, so patterns read as intended even while the rig turns.

### D4. Sequencer is arithmetic on elapsed time

`sequenceAt(elapsed)` maps elapsed seconds to `{ patternIndex, loop, phase }` by
walking a precomputed table of `5 × loopSeconds` blocks modulo the full cycle. No
timers or state, so it is trivially testable and never drifts.

### D5. Spot effects

Each pole has a phase offset; its brightness combines a slow fade
(`0.5 + 0.5·sin`) with a blink section in the cycle (square wave at ≤ 2 Hz). The
same value drives the three.js `SpotLight.intensity`, the lens emissive, and an
additive, transparent beam cone (`ConeGeometry`, open-ended) that makes the narrow beam
visible.

### D6. Bloom via `UnrealBloomPass`

`EffectComposer` + `RenderPass` + `UnrealBloomPass` + `OutputPass` from
`three/examples/jsm` (already shipped with the installed `three`). Threshold ≈ 1 so
only the HDR bulb colours and lenses bloom.

### D7. Flash-rate and reduced-motion limits enforced in code

A test samples every pattern and every spot effect at 240 Hz over several loops and
asserts at most 3 off→on transitions in any 1 s window (WCAG 2.3.1). Under
`prefers-reduced-motion` the loop renders a constant brightness (0.85) and does not
advance the sequencer.

## Risks / Trade-offs

- [GLB size ≈ 10 MB delays first paint of the model] → load asynchronously after the
  scene is up; the dark stage and camera are visible immediately. The model can later be
  Draco-compressed if needed.
- [Bloom costs fill rate on weak GPUs] → pixel ratio capped at 2 (as today); bloom
  resolution at half size.
- [Re-exporting the model drops hand edits to the GLB] → the GLB is a build artifact of
  the `.blend` + export script; never edit it by hand.
- [Name-based lookup of pivots breaks if the `.blend` renames them] → missing pivots are
  skipped (no crash) and a unit-tested finder documents the expected names.
