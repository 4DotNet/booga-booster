## ADDED Requirements

### Requirement: Commanded engine brake toggle

The drive (the central mill and all four hubs together) SHALL have a single
**brake-engaged** state, released by default, changed only through an
intent-revealing domain operation. Engaging the brake SHALL cut the commanded
drive power of the mill and every hub to zero. Releasing the brake SHALL leave the
commanded power at zero (the operator must command power again to drive the ride);
it SHALL NOT restore any previous power setting. Setting the brake to the state it
is already in SHALL be a no-op.

#### Scenario: A new ride has the brake released

- **WHEN** a ride is created
- **THEN** its engine brake is released

#### Scenario: Engaging the brake cuts drive power

- **WHEN** the mill and hubs are commanded to some non-zero power and the engine brake is engaged
- **THEN** the mill's commanded power and every hub's commanded power become zero
- **AND** the engine brake reports engaged

#### Scenario: Releasing the brake does not restore power

- **WHEN** the engine brake is engaged (power at zero) and it is then released
- **THEN** the engine brake reports released
- **AND** the mill's and hubs' commanded power remain zero

### Requirement: Brake torque decelerates the ride to a fast stop

While the engine brake is engaged, the simulation SHALL add a strong braking
torque — `RideParameters.MillBrakeTorque` for the mill and
`RideParameters.HubBrakeTorque` for each hub — to the drag opposing that body's
rotation, so that a moving mill and hubs decelerate and come to a complete rest
within a couple of simulated seconds. The braking torque SHALL only oppose motion
and SHALL NOT drive a body that is already at rest. While the brake is released the
ride SHALL rotate under its ordinary friction only, with no braking torque applied.

#### Scenario: An engaged brake stops a spinning ride quickly

- **WHEN** the ride is running at speed and the engine brake is engaged
- **THEN** the mill and hubs decelerate to a complete rest within a couple of simulated seconds

#### Scenario: A released brake applies no braking torque

- **WHEN** the ride is running at speed with the engine brake released
- **THEN** no braking torque is applied and the ride coasts or is driven under its ordinary friction only

#### Scenario: The brake never spins a stopped body

- **WHEN** the ride is at rest with the engine brake engaged
- **THEN** the mill and hubs remain at rest

### Requirement: The brake is applied automatically while stopping

Entering the `Stopping` state and the `Emergency Stop` state SHALL automatically
cut drive power to zero and engage the engine brake, so the ride comes to a
complete stop fast. When the ride reaches rest and enters `Offloading`, and when a
new run enters `Started`, the engine brake SHALL be released.

#### Scenario: Stopping engages the brake automatically

- **WHEN** the ride transitions to `Stopping`
- **THEN** the drive power is cut to zero
- **AND** the engine brake is engaged

#### Scenario: Emergency stop engages the brake automatically

- **WHEN** the ride transitions to `Emergency Stop`
- **THEN** the drive power is cut to zero
- **AND** the engine brake is engaged

#### Scenario: The brake is released once the ride is offloading

- **WHEN** a stopping ride reaches a complete rest and enters `Offloading`
- **THEN** the engine brake is released

#### Scenario: Starting a run releases the brake

- **WHEN** the ride transitions to `Started`
- **THEN** the engine brake is released

### Requirement: Telemetry reports the brake state

Ride telemetry SHALL report whether the engine brake is currently engaged, so
consumers can render the brake control's state truthfully.

#### Scenario: Telemetry reflects an engaged brake

- **WHEN** the engine brake is engaged
- **THEN** the ride telemetry reports the brake as engaged

#### Scenario: Telemetry reflects a released brake

- **WHEN** the engine brake is released
- **THEN** the ride telemetry reports the brake as released

### Requirement: Operator brake command

The API SHALL expose a command that sets the engine brake to an explicit engaged or
released state, going through the same command path as the other operator controls.
A request that engages the brake SHALL engage it; a request that releases it SHALL
release it.

#### Scenario: Operator engages the brake

- **WHEN** the operator sends an engine-brake command with engaged = true
- **THEN** the ride's engine brake becomes engaged and drive power is cut to zero

#### Scenario: Operator releases the brake

- **WHEN** the ride's engine brake is engaged and the operator sends an engine-brake command with engaged = false
- **THEN** the ride's engine brake becomes released

### Requirement: Brake toggle in the operator UI disables the power sliders

The operator dashboard SHALL present "Apply brakes" as a toggle whose pressed state
follows the reported brake state. While the brake is engaged the mill and hub power
sliders SHALL be disabled; when the brake is released the sliders SHALL be enabled
again (subject to the ride's existing lifecycle gating).

#### Scenario: Engaging the brake disables the sliders

- **WHEN** the operator engages the "Apply brakes" toggle
- **THEN** the toggle shows as pressed
- **AND** the mill and hub power sliders are disabled

#### Scenario: Releasing the brake re-enables the sliders

- **WHEN** the brake is engaged and the operator releases the "Apply brakes" toggle
- **THEN** the toggle shows as not pressed
- **AND** the mill and hub power sliders are enabled again
