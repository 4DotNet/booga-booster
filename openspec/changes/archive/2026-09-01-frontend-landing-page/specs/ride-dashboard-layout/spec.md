## ADDED Requirements

### Requirement: Dashboard is the landing page

The application SHALL render the ride dashboard as the default route (`''`) so it is the first view an operator sees.

#### Scenario: Navigating to the app root

- **WHEN** the operator loads the application at the root path
- **THEN** the ride dashboard is displayed without any further navigation

#### Scenario: Unknown route

- **WHEN** the operator navigates to a path that does not exist
- **THEN** the application redirects to the ride dashboard

### Requirement: Three-column dashboard layout

The dashboard SHALL present a left control rail, a central ride visualization, and a right telemetry rail. The central visualization SHALL be the visually dominant region, with the two rails flanking it.

#### Scenario: Wide viewport

- **WHEN** the dashboard is shown on a viewport at least 1024px wide
- **THEN** the left rail, central visualization, and right rail are laid out side by side, with the visualization occupying the largest share of the width

#### Scenario: Narrow viewport

- **WHEN** the dashboard is shown on a viewport narrower than 1024px
- **THEN** the columns reflow into a single vertically stacked column (visualization, then controls, then telemetry) without horizontal overflow

### Requirement: Status summary panel

The dashboard SHALL display a status summary panel at the top of the left rail showing the current ride state, the number of occupied seats, and the overall security state.

#### Scenario: Ride running with all seats secured

- **WHEN** the ride state is `running`, 40 seats are occupied, and every occupied seat is secured
- **THEN** the panel shows the state `running`, an occupied-seat count of 40, and an overall security state of `secured`

#### Scenario: A seat is not secured

- **WHEN** at least one occupied seat is not secured
- **THEN** the overall security state is shown as `unsecured` and is visually distinguished from the secured state by more than color alone

### Requirement: Central ride visualization

The dashboard SHALL render a visual representation of the ride showing the central mill and its 16 gondolas arranged around it, reflecting live rotation and per-gondola occupancy.

#### Scenario: Ride rotating

- **WHEN** the central mill has a non-zero rotation speed
- **THEN** the visualization animates the gondolas' rotation in the mill's current direction at a rate reflecting the mill speed

#### Scenario: Ride stopped

- **WHEN** the central mill rotation speed is zero
- **THEN** the visualization is static

#### Scenario: Occupancy shown per gondola

- **WHEN** a gondola has one or more occupied seats
- **THEN** the visualization visually indicates that gondola as occupied, distinct from empty gondolas

### Requirement: Accessible dashboard

The dashboard SHALL meet WCAG AA and pass AXE checks, including landmark structure, keyboard navigability, and non-color-dependent status indication.

#### Scenario: Automated accessibility audit

- **WHEN** an AXE audit is run against the rendered dashboard
- **THEN** no violations are reported

#### Scenario: Purely decorative visualization

- **WHEN** the central visualization conveys no information not already available in text panels
- **THEN** it is exposed to assistive technology as decorative (e.g. `aria-hidden`) so screen-reader users are not given redundant or unlabeled graphics
