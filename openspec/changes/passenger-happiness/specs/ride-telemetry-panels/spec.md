# Spec Delta

## ADDED Requirements

### Requirement: Rider experience panel

The telemetry rail SHALL include a Rider Experience panel placed directly below the gondola panel. It SHALL show four labelled progress bars on a 0–100 scale: Queue happiness (average happiness of everyone in the queue), Rider happiness (average happiness of everyone on the ride), Rider intensity (average preferred intensity of everyone on the ride) and Rider nausea (average nausea of everyone on the ride).

#### Scenario: Panel position

- **WHEN** the dashboard is rendered
- **THEN** the Rider Experience panel is the next panel after the gondola panel in the telemetry rail

#### Scenario: Bars show the reported averages

- **WHEN** the queue reports an average happiness of 72 and the telemetry reports rider averages of happiness 81, preferred intensity 74 and nausea 12
- **THEN** the four bars show 72, 81, 74 and 12 respectively, each with its numeric value visible

#### Scenario: No data available

- **WHEN** the queue is empty or nobody is on the ride
- **THEN** the affected bars show an explicit "no data" state rather than a value of 0

#### Scenario: Live updates

- **WHEN** a new telemetry frame or queue status arrives with different averages
- **THEN** the corresponding bars update without a page reload

### Requirement: Rider experience panel is accessible

Each progress bar in the Rider Experience panel SHALL expose its label, its current value and its 0–100 range to assistive technology, SHALL carry its numeric value as text rather than conveying it by bar length or colour alone, and the panel SHALL announce updates through a polite live region. The panel SHALL pass an automated axe audit at WCAG AA.

#### Scenario: Screen reader reads a bar

- **WHEN** assistive technology focuses the Rider nausea bar showing 12
- **THEN** it announces the label "Rider nausea" and the value 12 out of 100

#### Scenario: Automated audit

- **WHEN** the panel is rendered with data and with no data
- **THEN** an axe audit reports no WCAG AA violations in either state
