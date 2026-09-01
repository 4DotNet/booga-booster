## ADDED Requirements

### Requirement: Motor groups have a commanded rotation direction

The central mill and the hub motor group SHALL each carry a commanded rotation
direction that is either Forward or Reverse. A freshly created ride SHALL start
with both motor groups commanded Forward. The direction SHALL be independent of
motor power: changing power MUST NOT change direction, and changing direction
MUST NOT change power.

#### Scenario: Default direction on a new ride

- **WHEN** a ride is created
- **THEN** the mill's commanded direction is Forward
- **AND** every hub's commanded direction is Forward

#### Scenario: Direction is independent of power

- **WHEN** the operator sets the mill direction to Reverse
- **AND** then sets the mill power to any value
- **THEN** the mill's commanded direction remains Reverse
- **AND** the mill power reflects the newly commanded value

### Requirement: Operator can command the mill and hub direction

The system SHALL expose operations to set the central mill direction and to set
the hub motor group direction (applied identically to all four hubs). Each
operation SHALL be reachable from the operator UI through an API endpoint that
maps to the domain via a command handler. Setting a direction to the value it
already holds SHALL be a no-op that leaves the ride unchanged.

#### Scenario: Command the mill to run in reverse

- **WHEN** the operator commands the mill direction to Reverse
- **THEN** the ride's mill reports its commanded direction as Reverse

#### Scenario: Command the hubs to run in reverse

- **WHEN** the operator commands the hub direction to Reverse
- **THEN** all four hubs report their commanded direction as Reverse

#### Scenario: Setting the current direction changes nothing

- **WHEN** a motor group is already commanded Forward
- **AND** the operator commands it Forward again
- **THEN** the ride state is left unchanged

### Requirement: The commanded direction sets the sign of the drive torque

The rotational drive SHALL apply the commanded direction as the sign of the
motor's drive torque: Forward drives the body in the positive direction and
Reverse drives it in the negative direction. The magnitude of the drive torque
SHALL be unchanged by direction — a reversed motor is exactly as strong as a
forward one at the same power.

#### Scenario: A reversed motor spins a body up the opposite way from rest

- **WHEN** a body is at rest with non-zero power commanded Reverse
- **AND** the simulation advances
- **THEN** the body's angular velocity becomes negative and grows in magnitude
- **AND** its reported rotation speed is signed negative

### Requirement: Reversing while spinning decelerates through rest, then accelerates the other way

WHEN a motor group's direction is reversed while its body is already rotating, the drive torque SHALL oppose the current motion. The body SHALL first slow down, pass through a standstill, and then accelerate in the newly commanded direction. The reversal SHALL be continuous — the rotation speed MUST pass through zero rather than jump discontinuously to the opposite sign.

#### Scenario: A spinning body slows, stops, and reverses

- **WHEN** a body is rotating forward under power
- **AND** its direction is commanded Reverse while power stays applied
- **AND** the simulation advances over successive steps
- **THEN** its forward speed first decreases toward zero
- **AND** subsequently its speed increases in the reverse (negative) direction

#### Scenario: The turnaround is continuous

- **WHEN** a rotating body is commanded to reverse
- **THEN** across the reversal its angular velocity passes through (near) zero
- **AND** does not jump directly from a large positive value to a large negative value in a single step

### Requirement: Telemetry reports commanded direction and signed speed

Mill and hub telemetry SHALL report the commanded rotation direction. The
reported rotation speed SHALL be signed: positive while the body physically
turns forward and negative while it physically turns in reverse, so that a
consumer can render the true instantaneous turn direction — including the
transient while a reversing body is still coasting the old way.

#### Scenario: Reversed-and-running telemetry

- **WHEN** the mill is commanded Reverse and has spun up in reverse
- **THEN** its telemetry reports direction Reverse
- **AND** its reported speed is negative

#### Scenario: Commanded direction is reported even at rest

- **WHEN** a motor group is commanded Reverse while the ride is at rest
- **THEN** its telemetry reports direction Reverse
- **AND** its reported speed is zero

### Requirement: The dashboard reflects and commands direction

The operator controls SHALL send direction changes to the backend (not merely
echo them locally) and SHALL show each motor group's commanded direction from
telemetry. The 3D visualization SHALL turn each body according to the signed
telemetry speed, so that a commanded reversal is shown as a smooth slow-down
followed by rotation the other way.

#### Scenario: Toggling reverse reaches the backend

- **WHEN** the operator toggles a motor group to Reverse in the controls
- **THEN** a direction command is dispatched to the backend for that motor group

#### Scenario: Visualization follows the signed speed

- **WHEN** telemetry reports a negative rotation speed for a body
- **THEN** the visualization turns that body in the reverse direction
