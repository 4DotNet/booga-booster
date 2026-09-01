## ADDED Requirements

### Requirement: Ride lifecycle states

The ride SHALL have exactly one lifecycle state at any time, drawn from: **Idle**, **Loading**, **Safe**, **Started**, **Stopping**, **Offloading**, and **Emergency Stop**. A freshly created ride SHALL be **Idle**.

- **Idle** — the ride is at rest and empty, doing nothing.
- **Loading** — groups are being loaded into the gondolas and passengers are securing their restraints.
- **Safe** — everyone is loaded and every safety condition is met; the ride is safe to start.
- **Started** — the ride is running with safety constraints locked.
- **Stopping** — a controlled stop is in progress: power is cut and brakes are applied while the ride coasts to rest.
- **Offloading** — safety constraints are released and passengers leave the gondolas.
- **Emergency Stop** — an emergency stop is in progress: brakes are applied immediately from whatever active state the ride was in.

#### Scenario: A new ride starts Idle

- **WHEN** a ride is created
- **THEN** its lifecycle state is `Idle`
- **AND** the ride is at rest with no occupied seats

### Requirement: Guarded, deterministic transitions

The ride SHALL move between states only along the transitions defined by the state machine, and SHALL reject any transition that is not defined for the current state. The legal **operator-triggered** transitions are:

| From | To | Guard |
| --- | --- | --- |
| Idle | Loading | none |
| Loading | Safe | ride is safe (every occupied restraint secured, load balanced, not overloaded) |
| Safe | Loading | none (reopen loading) |
| Safe | Started | ride is safe |
| Started | Stopping | none |
| Loading / Safe / Started / Stopping | Emergency Stop | none |

Any transition request whose (from, to) pair is not in this table, or whose guard is not satisfied, SHALL be rejected without changing state.

#### Scenario: Legal transition is accepted

- **WHEN** the ride is `Idle` and a transition to `Loading` is requested
- **THEN** the ride's state becomes `Loading`

#### Scenario: Undefined transition is rejected

- **WHEN** the ride is `Idle` and a transition to `Started` is requested
- **THEN** the request is rejected with a domain validation error
- **AND** the ride's state remains `Idle`

#### Scenario: Guarded transition blocked when unsafe

- **WHEN** the ride is `Loading` with at least one occupied seat whose restraint is not secured
- **AND** a transition to `Safe` is requested
- **THEN** the request is rejected with a domain validation error
- **AND** the ride's state remains `Loading`

#### Scenario: Guarded transition allowed when safe

- **WHEN** the ride is `Loading`, every occupied restraint is secured, the load is balanced and within the maximum, and a transition to `Safe` is requested
- **THEN** the ride's state becomes `Safe`

### Requirement: Automatic condition-driven transitions

The simulation SHALL perform the following transitions automatically when their physical condition is met, without an operator command, each time the ride advances:

- **Safe → Loading** when the ride is no longer safe (e.g. a secured restraint opens or the load becomes unbalanced).
- **Stopping → Offloading** when the ride has come to a complete rest.
- **Emergency Stop → Offloading** when the ride has come to a complete rest; entering `Offloading` releases the safety constraints.
- **Offloading → Idle** when the last rider has left (every seat is empty).

#### Scenario: Safe demotes to Loading when safety is lost

- **WHEN** the ride is `Safe` and a secured restraint subsequently opens
- **THEN** on the next advance the ride's state becomes `Loading`

#### Scenario: Stopping settles into Offloading at rest

- **WHEN** the ride is `Stopping` and its rotation reaches a complete rest
- **THEN** the ride's state becomes `Offloading`

#### Scenario: Offloading returns to Idle when empty

- **WHEN** the ride is `Offloading` and the last occupied seat becomes empty
- **THEN** the ride's state becomes `Idle`

### Requirement: Emergency stop from any active state

The ride SHALL accept an Emergency Stop request from any of the active states `Loading`, `Safe`, `Started`, and `Stopping`. Entering `Emergency Stop` SHALL immediately cut engine power and apply the brakes. Once the ride has come to a complete rest it SHALL release the safety constraints and move to `Offloading`.

#### Scenario: Emergency stop while running

- **WHEN** the ride is `Started` and an Emergency Stop is requested
- **THEN** the ride's state becomes `Emergency Stop`
- **AND** engine power is cut and the brakes are applied

#### Scenario: Emergency stop releases constraints on coming to rest

- **WHEN** the ride is in `Emergency Stop` and reaches a complete rest
- **THEN** the safety constraints are released
- **AND** the ride's state becomes `Offloading`

#### Scenario: Emergency stop rejected when not in an active state

- **WHEN** the ride is `Idle` and an Emergency Stop is requested
- **THEN** the request is rejected with a domain validation error
- **AND** the ride's state remains `Idle`

### Requirement: Safety constraints and motion are confined by state

Safety constraints SHALL be locked on entering `Started` and SHALL only be released while the ride is at rest (on entering `Offloading`, whether reached from `Stopping` or `Emergency Stop`). The ride SHALL only be in motion while `Started`, `Stopping`, or `Emergency Stop`; in `Idle`, `Loading`, `Safe`, and `Offloading` the ride SHALL be at rest.

#### Scenario: Constraints lock when the ride starts

- **WHEN** the ride transitions from `Safe` to `Started`
- **THEN** the safety constraints are locked
- **AND** occupied restraints can no longer be opened

#### Scenario: Constraints are never released while moving

- **WHEN** the ride is `Started`, `Stopping`, or `Emergency Stop` and still rotating
- **THEN** the safety constraints remain locked

### Requirement: Telemetry exposes the state and legal transitions

The ride telemetry SHALL include the current lifecycle state and the set of operator-triggered transitions that are legal from that state right now (with their guards evaluated). Automatic transitions SHALL NOT appear in this set.

#### Scenario: Available transitions reflect the current state

- **WHEN** telemetry is read while the ride is `Safe` and safe to start
- **THEN** the available transitions include `Started`, `Loading`, and `Emergency Stop`
- **AND** they do not include `Idle` or `Offloading`

#### Scenario: Guarded transition omitted when its guard fails

- **WHEN** telemetry is read while the ride is `Loading` and an occupied restraint is unsecured
- **THEN** the available transitions do not include `Safe`

### Requirement: Server endpoint requests a transition

The DigitalTwin module SHALL expose an HTTP endpoint that accepts a requested target state and applies it to the ride through the state machine. A legal, guard-satisfying request SHALL be accepted; an illegal or guard-failing request SHALL return a client error and leave the state unchanged. An unrecognized target state SHALL return a client error.

#### Scenario: Legal transition request accepted

- **WHEN** a client posts a transition to `Loading` while the ride is `Idle`
- **THEN** the endpoint responds with success
- **AND** subsequent telemetry reports the state as `Loading`

#### Scenario: Illegal transition request rejected

- **WHEN** a client posts a transition to `Started` while the ride is `Idle`
- **THEN** the endpoint responds with a client error
- **AND** subsequent telemetry still reports the state as `Idle`

#### Scenario: Unknown target state rejected

- **WHEN** a client posts a transition to an unrecognized state name
- **THEN** the endpoint responds with a client error

### Requirement: Status-summary panel displays and drives the state

The status-summary panel SHALL display the current ride state and render one button per lifecycle state. A button SHALL be enabled ("lit") only when a transition to that state is currently legal (i.e. it appears in the telemetry's available transitions); all other state buttons SHALL be disabled. Enabling and disabling SHALL be conveyed by more than colour alone and the panel SHALL pass AXE / WCAG AA checks. Activating a lit button SHALL send the corresponding transition request to the server, and the panel SHALL reflect the resulting state once the server confirms it.

#### Scenario: Only legal transitions are lit

- **WHEN** the ride is `Safe` and safe to start
- **THEN** the `Started`, `Loading`, and `Emergency Stop` buttons are enabled
- **AND** the `Idle`, `Stopping`, and `Offloading` buttons are disabled

#### Scenario: Clicking a lit button drives the transition on the server

- **WHEN** the operator activates the enabled `Started` button while the ride is `Safe`
- **THEN** a transition request to `Started` is sent to the server
- **AND** the displayed state updates to `Started` when the server reports it

#### Scenario: Disabled buttons cannot be activated

- **WHEN** the ride is `Idle`
- **THEN** the `Started` button is disabled and cannot send a transition request
