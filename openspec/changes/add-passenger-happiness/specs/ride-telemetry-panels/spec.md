# Spec Delta

## ADDED Requirements

### Requirement: Rider Experience panel

The telemetry rail SHALL include a Rider Experience panel placed directly under the gondola panel. It SHALL show four labelled progress bars, in this order: Queue happiness (average happiness of everyone waiting), Rider happiness (average happiness of everyone on the ride), Rider intensity (average preferred ride intensity of everyone on the ride) and Rider nausea (average nausea of everyone on the ride). Each bar SHALL run from 0 to 100.

#### Scenario: Panel position

- **WHEN** the dashboard is rendered
- **THEN** the Rider Experience panel appears in the telemetry rail immediately after the gondola panel

#### Scenario: Bars show the averages

- **WHEN** the queue's average happiness is 72.4 and the riders' averages are happiness 81.6, preferred intensity 74.9 and nausea 12.2
- **THEN** the panel shows Queue happiness 72, Rider happiness 82, Rider intensity 75 and Rider nausea 12, each as a filled bar with its value visible as text

#### Scenario: Bars follow live updates

- **WHEN** a new telemetry frame or queue status changes one of the averages
- **THEN** the matching bar and its value update without a page reload

### Requirement: Rider Experience panel distinguishes no data from zero

When there is nobody to average, the Rider Experience panel SHALL say so instead of showing an empty bar. Queue happiness SHALL show a "No guests waiting" message when the queue is empty. The three rider bars SHALL show a "No riders" message when nobody is aboard.

#### Scenario: Empty queue

- **WHEN** nobody is waiting in the queue
- **THEN** the Queue happiness row shows "No guests waiting" instead of a bar at 0

#### Scenario: Empty ride

- **WHEN** nobody is aboard the ride
- **THEN** the Rider happiness, Rider intensity and Rider nausea rows show "No riders" instead of bars at 0

### Requirement: Rider Experience panel is accessible

Each progress bar in the Rider Experience panel SHALL expose to assistive technology an accessible name (its label), its current value, and its 0–100 range. The value text SHALL be read as points out of 100. The panel SHALL pass AXE / WCAG AA checks both with values and in its no-data states. Value changes SHALL be announced politely.

#### Scenario: Screen reader reads a bar

- **WHEN** a screen-reader user focuses or browses the Rider nausea bar showing 12
- **THEN** it is announced as a progress bar named "Rider nausea" with the value 12 out of 100

#### Scenario: Automated accessibility check

- **WHEN** axe-core is run against the panel with values and with no data
- **THEN** it reports no violations
