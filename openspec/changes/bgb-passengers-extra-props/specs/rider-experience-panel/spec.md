# Spec Delta

## Purpose

Defines the Rider Experience panel on the operator dashboard. It shows at a glance how happy the waiting guests are, and how happy, how thrill-seeking and how nauseous the current riders are.

## ADDED Requirements

### Requirement: Rider Experience panel sits under the Gondolas panel

The right telemetry rail SHALL include a panel titled "Rider Experience", placed directly below the Gondolas panel.

#### Scenario: Panel placement

- **WHEN** the dashboard is rendered
- **THEN** the right rail shows the Rider Experience panel immediately after the Gondolas panel

### Requirement: Panel shows four experience progress bars

The panel SHALL show four labelled progress bars on a 0–100 scale:

- "Queue happiness": the average happiness of everyone in the queue.
- "Rider happiness": the average happiness of everyone on the ride.
- "Rider intensity": the average preferred ride intensity of everyone on the ride.
- "Rider nausea": the average nausea of everyone on the ride.

#### Scenario: Values from live data

- **WHEN** the queue reports an average happiness of 72 and the ride reports average happiness 81, preferred intensity 68 and nausea 12
- **THEN** the four bars show 72, 81, 68 and 12 respectively

#### Scenario: Live updates

- **WHEN** a new telemetry frame or queue status arrives with changed averages
- **THEN** the corresponding bars update without a page reload

### Requirement: Absent data is shown explicitly

When nobody is on the ride, the three rider bars SHALL show an explicit "no riders" state instead of a 0 value. When the queue is empty, the queue happiness bar SHALL show an explicit "queue empty" state. An absent value SHALL NOT be displayed as 0.

#### Scenario: Empty ride

- **WHEN** no passengers are boarded
- **THEN** the rider happiness, intensity and nausea bars indicate that there are no riders, and none of them reads 0

#### Scenario: Empty queue

- **WHEN** no guests are waiting
- **THEN** the queue happiness bar indicates that the queue is empty

### Requirement: Rider Experience panel is accessible

The panel SHALL meet WCAG AA and pass AXE checks. Each bar SHALL have an accessible name that includes its label and its value with a percent unit, and SHALL expose its 0–100 range to assistive technology. Updates SHALL be announced politely, like the other telemetry panels.

#### Scenario: Automated accessibility audit

- **WHEN** an AXE audit is run against the rendered panel, both with data and in its empty states
- **THEN** no violations are reported

#### Scenario: Screen reader reads a bar

- **WHEN** a screen-reader user focuses the rider nausea bar showing 12
- **THEN** it is announced with its label and the value "12%"
