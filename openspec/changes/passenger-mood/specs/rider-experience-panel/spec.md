## ADDED Requirements

### Requirement: Rider Experience panel in the right rail

The dashboard SHALL show a Rider Experience panel in the right telemetry rail, directly below the Gondolas panel, containing four labelled progress bars:

- **Queue happiness** — average happiness of all queued guests, on a 0–100 scale.
- **Rider happiness** — average happiness of all seated riders, on a 0–100 scale.
- **Rider preferred G** — average preferred G of all seated riders, on a 0 g – `MaxGForce` scale, labelled in g.
- **Rider nausea** — average nausea of all seated riders, on a 0–100 scale.

Queue happiness SHALL update from the queue poll; the rider bars SHALL update from the telemetry stream.

#### Scenario: Averages are shown
- **WHEN** two riders are seated with happiness 60 and 80
- **THEN** the Rider happiness bar shows 70

#### Scenario: Preferred G is shown in g
- **WHEN** the seated riders' average preferred G is 3.3 g
- **THEN** the Rider preferred G bar is filled to 3.3 / 4.5 of its width and its label reads `3.3 g`

#### Scenario: Empty population shows no value
- **WHEN** nobody is seated
- **THEN** the three rider bars show an empty bar with the value text `—` instead of `0`

### Requirement: Mood counts are reported as text

The panel SHALL state as text the number of mad guests in the queue (happiness < 30), the number of sick riders on the ride (nausea ≥ 70), and the number of sick riders in the last offload. These counts are the accessible equivalent of the mood colours and puke effect in the 3D scene.

#### Scenario: Mad guests are counted
- **WHEN** 3 queued guests have happiness below 30
- **THEN** the panel reads `3 mad in queue`

#### Scenario: Sick riders on the last offload are counted
- **WHEN** the last offload contained 2 riders with nausea of 70 or more
- **THEN** the panel reads `2 left sick`

### Requirement: Rider Experience panel is accessible

The panel SHALL pass AXE / WCAG AA. Every progress bar SHALL expose its label and current value to assistive technology, and changes SHALL NOT be announced more often than the existing telemetry panels announce.

#### Scenario: Bars have accessible names and values
- **WHEN** the panel is rendered
- **THEN** each progress bar has an accessible name matching its visible label and exposes its value, and an axe-core check reports no violations
