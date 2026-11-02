## Why

The dashboard's 3D view is still a set of placeholder boxes on a 6 m tower, while the
ride's real design now lives in `designs/3d/ride-design.blend`: a low-slung rig with
sit-on gondolas, bulb strings on every arm, a half-round artwork backdrop and eight
light poles. A carnival ride is sold by its lights, so the twin should show the real
design and put on a light show from the moment the app opens.

## What Changes

- The dashboard renders the designed ride model (exported from the Blender design as a
  single web model) in place of the hand-built boxes: low rig, two-seat gondolas on
  top of the hub arms, half-round backdrop with the Booga Booster artwork, floor and
  eight light poles. The mill, hubs and gondolas keep turning from live telemetry
  exactly as today.
- A light show drives every decorative bulb (arms, hubs, crown, base, gondola rims,
  backdrop and poles). It has **12 patterns**; from app start each pattern plays
  **5 loops**, then the next pattern starts, cycling through all 12 forever.
- Each pole carries a narrow-beam spotlight in a theme colour (pink, cyan, gold,
  purple) aimed at the ride; the spots **blink and fade** and light both the ride and
  the backdrop behind it.
- The show never flashes faster than 3 times per second, and under
  `prefers-reduced-motion` the bulbs and spots hold a calm, steady glow.
- A reproducible export script turns the `.blend` into the web model.
- **BREAKING (visual only):** the 3D geometry, camera framing and lighting of the
  visualization change; no API or data contract changes.

## Capabilities

### New Capabilities

- `ride-light-show`: the decorative bulb patterns, their sequencing (5 loops each,
  cycling through 12), the blinking/fading pole spotlights, and their safety limits
  (flash rate, reduced motion).
- `ride-3d-scenery`: the visualization shows the designed ride model with its
  backdrop and light poles, keeps the rig animated from telemetry, and degrades
  safely when the model or WebGL is unavailable.

### Modified Capabilities

<!-- none: the central-visualization requirements in ride-dashboard-layout and
motor-direction-control (rotation follows telemetry, static when stopped) stay as
they are and keep holding for the new model. -->

## Impact

- Angular app: `ride-dashboard/visualization/` (the visualization component is
  reworked; new light-show and scenery modules with Vitest specs).
- New static asset `public/3dmodels/booga-booster.glb` (≈ 10 MB incl. an 8192×2048
  backdrop texture); the unused `public/3dmodels/boogabooster.fbx` is removed.
- New `designs/3d/export_web_model.py` (run inside Blender).
- No backend, API or telemetry changes. No new npm dependencies (three.js already
  ships the GLTF loader and the bloom post-processing passes).
