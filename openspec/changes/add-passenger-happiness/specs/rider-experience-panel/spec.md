# Spec Delta

## Purpose

Gives the operator an at-a-glance view of guest mood: a Rider Experience panel in the dashboard's right rail showing queue happiness and the riders' happiness, preferred intensity and nausea as progress bars.

## ADDED Requirements

### Requirement: Rider Experience panel sits under the Gondolas panel

The dashboard SHALL show a panel headed "Rider Experience" in the right telemetry rail, placed directly below the Gondolas panel.

#### Scenario: Panel placement

- **WHEN** the dashboard is rendered
- **THEN** the last panel in the right rail is "Rider Experience", immediately following the Gondolas panel

### Requirement: Panel shows four mood progress bars

The panel SHALL show four labelled progress bars on a 0–100 scale, in this order: "Queue happiness" (average happiness of everyone in the queue), "Rider happiness" (average happiness of everyone on the ride), "Rider intensity" (average preferred intensity of everyone on the ride) and "Rider nausea" (average nausea of everyone on the ride).

#### Scenario: Bars reflect the live averages

- **WHEN** the queue's average happiness is 72 and the ride's riders average happiness 64, preferred intensity 81 and nausea 12
- **THEN** the bars read 72, 64, 81 and 12 respectively, each with its label and a visible rounded value

#### Scenario: Values update live

- **WHEN** a new ride telemetry frame or queue status arrives with changed averages
- **THEN** the corresponding bars update without a page reload

### Requirement: Absent averages are shown as no data

When there is nobody in the queue, or nobody on the ride, the affected bars SHALL indicate that there are no guests instead of showing a value of 0.

#### Scenario: Empty ride

- **WHEN** nobody is aboard the ride
- **THEN** the rider happiness, rider intensity and rider nausea bars each show "No guests" and no numeric value

#### Scenario: Empty queue

- **WHEN** nobody is waiting in the queue
- **THEN** the queue happiness bar shows "No guests" and no numeric value

### Requirement: Rider Experience panel is accessible

The panel SHALL meet WCAG AA and pass AXE checks. Each bar SHALL expose an accessible name matching its label and its current value (or its no-data state) as text, so no information is conveyed by bar length or colour alone.

#### Scenario: Automated accessibility audit

- **WHEN** an AXE audit is run against the rendered panel, both with values and with no guests
- **THEN** no violations are reported

#### Scenario: Screen reader announces each metric

- **WHEN** a screen reader reaches the rider nausea bar while its value is 12
- **THEN** it announces the name "Rider nausea" and the value 12 out of 100
