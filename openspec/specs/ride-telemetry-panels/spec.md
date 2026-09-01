# ride-telemetry-panels Specification

## Purpose

Defines the security, rotation-speed, and gondola telemetry panels and how their updates are announced accessibly.

## Requirements

### Requirement: Security telemetry panel

The telemetry rail SHALL include a security panel showing the total occupied-seat count and, per seat, whether its restraint is closed and secured.

#### Scenario: Mixed seat states

- **WHEN** some occupied seats are secured and others are not
- **THEN** the panel shows the occupied-seat total and marks each seat with a distinct, non-color-only indicator for secured versus unsecured

#### Scenario: Empty seat

- **WHEN** a seat is not occupied
- **THEN** the panel indicates the seat as empty rather than secured or unsecured

### Requirement: Rotation speed telemetry panel

The telemetry rail SHALL include a speed panel showing the main mill rotation speed and the individual rotation speed of each hub.

#### Scenario: Hubs at differing speeds

- **WHEN** hubs report different rotation speeds due to differing loads
- **THEN** the panel shows the main mill speed and each hub's own speed with its unit

#### Scenario: Live speed updates

- **WHEN** a hub's reported rotation speed changes
- **THEN** the panel updates that hub's displayed speed without a full page reload

### Requirement: Gondola telemetry panel

The telemetry rail SHALL include a gondola panel listing all 16 gondolas; for each gondola it SHALL show which seats are occupied and the gondola's vertical and lateral g-forces.

#### Scenario: Occupied gondola under load

- **WHEN** a gondola has occupied seats and is experiencing a vertical g-force of +2.0 g and a lateral g-force of -0.5 g
- **THEN** that gondola's row shows its occupied seats, its vertical g-force with sign (push-back vs push-forward), and its lateral g-force with sign

#### Scenario: All gondolas listed

- **WHEN** the gondola panel is rendered
- **THEN** exactly 16 gondolas are shown, each individually addressable/identifiable

### Requirement: Telemetry updates are announced accessibly

Live-updating telemetry regions SHALL be exposed to assistive technology such that changes can be perceived without overwhelming the user (e.g. polite live regions), and numeric values SHALL always carry their unit in the accessible name.

#### Scenario: Screen reader on speed change

- **WHEN** a rotation speed value updates and the region is a polite live region
- **THEN** the change is available to assistive technology without interrupting the operator's current action

#### Scenario: Value has a unit

- **WHEN** any telemetry value (speed or g-force) is presented
- **THEN** its unit is included so the value is unambiguous to sighted and screen-reader users alike
