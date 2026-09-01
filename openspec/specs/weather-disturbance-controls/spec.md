# weather-disturbance-controls Specification

## Purpose

Defines the precipitation and strong-wind trigger buttons, their pending/feedback states, and their accessibility requirements.

## Requirements

### Requirement: Precipitation and strong-wind buttons

The weather panel SHALL provide two buttons that let the operator disturb the weather: one that starts precipitation and one that summons strong wind. Each SHALL invoke the corresponding server operation.

#### Scenario: Operator starts precipitation

- **WHEN** the operator activates the precipitation button
- **THEN** the app starts precipitation on the server (default type `Rain`)
- **AND** the displayed conditions refresh to show the precipitation

#### Scenario: Operator summons strong wind

- **WHEN** the operator activates the strong-wind button
- **THEN** the app starts a strong-wind event on the server
- **AND** the displayed conditions refresh to show the strong wind

### Requirement: Pending and feedback state

While a disturbance request is in flight the triggering button SHALL indicate the pending state and prevent duplicate submissions, and a failure SHALL be reported to the operator without breaking the panel.

#### Scenario: Button shows pending and blocks re-entry

- **WHEN** a disturbance request is in flight
- **THEN** the triggering button is disabled and marked busy until it completes

#### Scenario: Failure is surfaced

- **WHEN** a disturbance request fails
- **THEN** the operator is shown a failure message
- **AND** the button returns to its normal, usable state

### Requirement: Accessible controls

The buttons SHALL be real, keyboard-operable controls with clear accessible names and visible focus, meeting WCAG AA / AXE.

#### Scenario: Buttons are keyboard operable with clear names

- **WHEN** the operator navigates with the keyboard
- **THEN** each button is focusable with a visible focus indicator and an accessible name that states its effect (e.g. "Start precipitation", "Summon strong wind")
