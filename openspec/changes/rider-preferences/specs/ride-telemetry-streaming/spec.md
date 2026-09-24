## MODIFIED Requirements

### Requirement: Full telemetry payload

Each streamed frame SHALL be a complete ride snapshot, not a partial delta. It SHALL include, per the `RideTelemetry` contract: the ride lifecycle state; the total count of boarded passengers currently on the ride; the rider-mood roll-up (rider count, average happiness and average nausea of the seated passengers, the averages absent when nobody is seated); the mill's commanded power, sensed RPM, total and passenger load, imbalance and balance/overload flags; each of the four hubs' power, RPM and load; and each of the sixteen gondolas' brake state, yaw angle, RPM, forward and lateral G, load, and per-seat weight, restraint state, whether the seat is occupied, and whether its restraint is secured.

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

#### Scenario: Frame carries the rider-mood roll-up

- **WHEN** a telemetry frame is emitted while passengers are on the ride
- **THEN** the frame carries the rider count, the average rider happiness and the average rider nausea
- **AND** the rider count equals the boarded-passenger count

#### Scenario: Rider-mood averages are absent on an empty ride

- **WHEN** a telemetry frame is emitted while nobody is seated
- **THEN** the frame's rider count is `0` and its average happiness and nausea are absent
