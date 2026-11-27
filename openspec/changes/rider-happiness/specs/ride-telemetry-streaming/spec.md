# Spec Delta

## MODIFIED Requirements

### Requirement: Full telemetry payload

Each streamed frame SHALL be a complete ride snapshot, not a partial delta. It SHALL include, per the `RideTelemetry` contract: the ride lifecycle state; the total count of boarded passengers currently on the ride; the mill's commanded power, sensed RPM, total and passenger load, imbalance and balance/overload flags; each of the four hubs' power, RPM and load; each of the sixteen gondolas' brake state, yaw angle, RPM, forward and lateral G, load, and per-seat weight, restraint state, whether the seat is occupied, and whether its restraint is secured; and the rider-experience summary (rider count and average happiness, preferred intensity and nausea).

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

#### Scenario: Frame carries the rider-experience summary

- **WHEN** a telemetry frame is emitted
- **THEN** it carries the rider-experience summary
- **AND** the summary's rider count equals the frame's boarded-passenger count
- **AND** the averages are absent when that count is 0
