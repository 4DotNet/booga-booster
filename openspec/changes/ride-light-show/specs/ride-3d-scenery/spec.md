## Purpose

Defines what the central 3D visualization shows: the designed ride model with its
backdrop and light poles, animated from live telemetry, and how it degrades when the
model or WebGL is unavailable.

## ADDED Requirements

### Requirement: Designed ride model

The visualization SHALL show the ride as designed in `designs/3d/ride-design.blend`: a
low-slung rig with a central mill carrying four smooth arms, a hub at the end of each
arm, and on each hub four arms carrying two-seat gondolas that sit on top of the hub
arms — 16 gondolas in total.

#### Scenario: Model shown

- **WHEN** the visualization has loaded
- **THEN** the mill, four arms, four hubs and 16 two-seat gondolas of the designed model are visible

### Requirement: Backdrop and light poles

The visualization SHALL show a half-round backdrop behind the ride carrying the Booga
Booster artwork and lined with decorative bulbs, and eight light poles around the ride,
each topped by a spotlight.

#### Scenario: Scenery shown

- **WHEN** the visualization has loaded and the default camera view is shown
- **THEN** the backdrop artwork is visible behind the ride and the light poles stand around it

### Requirement: Rig follows telemetry

The designed model's mill, hubs and gondolas SHALL turn exactly as the current
visualization's rig does: the mill and hubs at their signed sensed speeds, and the
gondolas free-running while the brake is released and held while it is engaged.

#### Scenario: Ride rotating

- **WHEN** the mill and hub speeds are non-zero
- **THEN** the mill and hubs of the model turn in the direction given by the sign of their speeds, at a rate reflecting those speeds

#### Scenario: Brake engaged

- **WHEN** the gondola brake is engaged
- **THEN** each gondola holds its current angle relative to its hub arm

### Requirement: Safe degradation

The visualization SHALL remain a harmless, decorative element when it cannot render:
without WebGL it stays empty, and if the model fails to load it shows the empty lit
scene without raising an error to the operator.

#### Scenario: No WebGL

- **WHEN** the visualization mounts where WebGL is unavailable
- **THEN** it renders an empty element hidden from assistive technology and does not attempt to load the model

#### Scenario: Model fails to load

- **WHEN** the model file cannot be fetched or parsed
- **THEN** the rest of the dashboard keeps working and no error dialog is shown
