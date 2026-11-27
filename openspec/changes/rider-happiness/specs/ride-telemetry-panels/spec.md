# Spec Delta

## ADDED Requirements

### Requirement: Rider experience panel

The telemetry rail SHALL include a Rider Experience panel directly below the gondola panel. It SHALL show four labelled progress bars on a 0–100 scale: Queue happiness (average happiness of everyone in the queue), Rider happiness, Rider intensity (average preferred intensity) and Rider nausea, the last three averaged over everyone on the ride.

#### Scenario: Panel placement

- **WHEN** the dashboard is rendered
- **THEN** the Rider Experience panel is the panel directly below the gondola panel in the right-hand telemetry rail

#### Scenario: Bars show the averages

- **WHEN** the queue's average happiness is 72.4 and the ride's averages are happiness 81.6, preferred intensity 64.2 and nausea 12.5
- **THEN** the four bars are filled to those values
- **AND** each bar shows its label and its value rounded to a whole number out of 100

#### Scenario: No data for an empty queue or ride

- **WHEN** the queue is empty, or nobody is on the ride
- **THEN** the affected bars are replaced by a "no data" indication instead of showing 0

### Requirement: Rider experience bars are accessible

Each Rider Experience bar SHALL expose an accessible progressbar with an accessible name from its visible label and a value text of the form "N out of 100". The panel SHALL announce updates through a polite live region and SHALL pass AXE / WCAG AA checks.

#### Scenario: Screen reader reads a bar

- **WHEN** assistive technology focuses the Rider nausea bar while the average nausea is 12.5
- **THEN** it announces "Rider nausea" with the value "13 out of 100"

#### Scenario: Automated accessibility check

- **WHEN** the axe accessibility spec runs against the panel with data and without data
- **THEN** it reports no violations
