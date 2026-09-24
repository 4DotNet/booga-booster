## ADDED Requirements

### Requirement: Rider mood panel in the right telemetry rail

The dashboard SHALL show a **Rider mood** panel in the right telemetry rail, positioned directly below the Gondolas panel. The panel SHALL present three metrics: the average happiness of the people in the queue, the average happiness of the people on the ride, and the average nausea of the people on the ride.

#### Scenario: Panel position

- **WHEN** the dashboard is rendered
- **THEN** a panel headed "Rider mood" is the last panel of the right rail, immediately after the Gondolas panel

#### Scenario: Three metrics are shown

- **WHEN** the panel is rendered
- **THEN** it shows a "Queue happiness" metric, a "Rider happiness" metric and a "Nausea" metric, each labelled

### Requirement: Metrics reflect the live feeds

Queue happiness SHALL come from the polled queue status' average happiness. Rider happiness and nausea SHALL come from the rider-mood roll-up of the latest telemetry frame. Each value in `[0, 1]` SHALL be displayed as a whole-number percentage alongside a proportional meter, and SHALL update as new data arrives.

#### Scenario: Queue happiness follows the queue poll

- **WHEN** the queue status reports an average happiness of `0.72`
- **THEN** the Queue happiness metric shows `72 %`

#### Scenario: Rider metrics follow the telemetry frame

- **WHEN** a telemetry frame reports an average rider happiness of `0.6` and an average nausea of `0.25`
- **THEN** the Rider happiness metric shows `60 %` and the Nausea metric shows `25 %`

#### Scenario: Metrics update live

- **WHEN** a later frame reports different averages
- **THEN** the displayed values change accordingly

### Requirement: Empty and unavailable states are spelled out

When the queue is empty, the Queue happiness metric SHALL read "Queue empty". When nobody is on the ride, the Rider happiness and Nausea metrics SHALL read "No riders". When the queue feed is unavailable, the Queue happiness metric SHALL read "Unavailable". No state SHALL be conveyed by colour alone.

#### Scenario: Empty queue

- **WHEN** the queue status reports no average happiness
- **THEN** the Queue happiness metric reads "Queue empty"

#### Scenario: Empty ride

- **WHEN** the telemetry frame reports a rider count of `0`
- **THEN** the Rider happiness and Nausea metrics read "No riders"

#### Scenario: Queue feed down

- **WHEN** the queue source reports an error
- **THEN** the Queue happiness metric reads "Unavailable"

### Requirement: Accessible rider mood panel

The panel SHALL be a labelled region whose metrics are exposed to assistive technology with an accessible name that includes the metric's name and its value, and SHALL pass the automated axe-core WCAG AA audit. The panel SHALL use PrimeNG components for its meters.

#### Scenario: Automated accessibility audit

- **WHEN** the panel is rendered with values and with empty states
- **THEN** the axe-core audit reports no WCAG AA violations

#### Scenario: Screen reader reads a metric

- **WHEN** a screen reader reaches the Rider happiness metric showing `60 %`
- **THEN** it announces the metric name and the value `60 %`
