# ride-operation-controls Specification

## Purpose

Defines the operator's motor power and direction controls, and how they reflect and constrain themselves to the current ride state.

## Requirements

### Requirement: Motor power controls

The operation controls SHALL provide a power slider for the central mill motor and a power slider for the hub motors. Each slider SHALL let the operator set power from 0% to 100% and SHALL display the current power value.

#### Scenario: Operator sets mill power

- **WHEN** the operator moves the central mill power slider to 60%
- **THEN** the slider displays 60% and a `set-mill-power` command carrying the value 60 is emitted to the ride-state service

#### Scenario: Operator sets hub power

- **WHEN** the operator moves the hub power slider to 45%
- **THEN** the slider displays 45% and a `set-hub-power` command carrying the value 45 is emitted to the ride-state service

#### Scenario: Power bounds enforced

- **WHEN** a power value below 0 or above 100 is requested
- **THEN** the value is clamped to the 0–100 range before any command is emitted

### Requirement: Motor direction controls

The operation controls SHALL provide a direction toggle for the central mill motor and a direction toggle for the hub motors, each switching between forward and reverse.

#### Scenario: Operator reverses the mill

- **WHEN** the operator activates the central mill direction toggle while it is forward
- **THEN** the toggle shows reverse and a `set-mill-direction` command carrying `reverse` is emitted

#### Scenario: Operator reverses the hubs

- **WHEN** the operator activates the hub direction toggle while it is forward
- **THEN** the toggle shows reverse and a `set-hub-direction` command carrying `reverse` is emitted

### Requirement: Controls reflect current ride state

The controls SHALL initialize from and stay consistent with the ride-state service's current power and direction values.

#### Scenario: State changes elsewhere

- **WHEN** the ride-state service reports a mill power different from the slider's displayed value
- **THEN** the mill power slider updates to match the reported value

### Requirement: Accessible controls

Every slider and toggle SHALL be keyboard operable and expose an accessible name, current value, and role to assistive technology.

#### Scenario: Keyboard adjustment of a slider

- **WHEN** a power slider has focus and the operator presses the arrow keys
- **THEN** the power value changes in the corresponding direction and the new value is announced to assistive technology

#### Scenario: Toggle state exposed

- **WHEN** assistive technology inspects a direction toggle
- **THEN** the toggle reports its pressed/unpressed (forward/reverse) state and an accessible label identifying which motor it controls
