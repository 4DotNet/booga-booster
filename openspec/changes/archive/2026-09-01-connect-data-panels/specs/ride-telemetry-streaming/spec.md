## MODIFIED Requirements

### Requirement: Full telemetry payload

Each streamed frame SHALL be a complete ride snapshot, not a partial delta. It SHALL include, per the `RideTelemetry` contract: the ride lifecycle state; the total count of boarded passengers currently on the ride; the mill's commanded power, sensed RPM, total and passenger load, imbalance and balance/overload flags; each of the four hubs' power, RPM and load; and each of the sixteen gondolas' brake state, yaw angle, RPM, forward and lateral G, load, and per-seat weight, restraint state, whether the seat is occupied, and whether its restraint is secured.

#### Scenario: Frame carries mill and hub engine readings

- **WHEN** a telemetry frame is emitted while the ride is running
- **THEN** it contains the mill's power and RPM
- **AND** it contains the power and RPM of all four hubs

#### Scenario: Frame carries cart and seat state

- **WHEN** a telemetry frame is emitted while the ride is running
- **THEN** each gondola's brake, angle, RPM, forward/lateral G and load are present
- **AND** each seat's measured weight and restraint state are present
- **AND** each seat reports whether it is occupied and whether its restraint is secured

#### Scenario: Frame carries the boarded-passenger count

- **WHEN** a telemetry frame is emitted while passengers are on the ride
- **THEN** the frame carries the total count of boarded passengers
- **AND** the count equals the number of occupied seats across all gondolas

### Requirement: Emission gated on an active ride

The stream SHALL emit telemetry frames whenever the ride is active — that is, in any lifecycle state other than `Idle`, including `Loading`, `Safe`, `Offloading`, `Started`, `Stopping`, and `EmergencyStop` — so that boarding progress, accumulating load and securing restraints are observable live and not only once the ride is running. While the ride is `Idle` the connection SHALL stay open but no telemetry frames SHALL be emitted. When the ride leaves `Idle`, frames SHALL begin; when it returns to `Idle`, frames SHALL cease.

#### Scenario: No frames while the ride is idle

- **WHEN** a client is connected and the ride is `Idle`
- **THEN** no telemetry frames are emitted
- **AND** the connection remains open

#### Scenario: Frames during loading show boarding progress

- **WHEN** a client is connected and the ride is `Loading` as passengers board
- **THEN** telemetry frames are emitted at the telemetry rate
- **AND** successive frames reflect the rising passenger count, accumulating load and restraints securing over time

#### Scenario: Frames continue through running and offloading

- **WHEN** the ride is `Safe`, `Started`, `Stopping`, `EmergencyStop` or `Offloading` while a client is connected
- **THEN** telemetry frames continue to be emitted at the telemetry rate

#### Scenario: Frames cease when the ride returns to idle

- **WHEN** the ride returns to `Idle` while a client is connected
- **THEN** telemetry frames cease
- **AND** the connection remains open for a later run

## ADDED Requirements

### Requirement: Panels report boarded passengers and weight from the stream

The load/security panel SHALL display the boarded-passenger count and total weight sourced from the streamed telemetry frames, updating on every frame throughout loading and running. The passenger count SHALL be taken from the telemetry's boarded-passenger count (or, equivalently, the per-seat occupancy flags) rather than being inferred solely from a non-zero seat weight, and the weight SHALL reflect the streamed load. The security roll-up SHALL show a seat as occupied-but-unsecured while its restraint has not yet secured.

#### Scenario: Passenger count and weight update during loading

- **WHEN** the ride is `Loading` and telemetry frames arrive as passengers board
- **THEN** the panel's boarded-passenger count and weight-in-kilos increase to match the streamed frame
- **AND** they continue to update on every subsequent frame

#### Scenario: Occupied-but-unsecured is shown during the restraint countdown

- **WHEN** a seat is occupied but its restraint has not yet secured
- **THEN** the panel's security roll-up reports the seat as occupied and not yet secured
- **AND** once the restraint secures on a later frame, the roll-up reports it as secured
