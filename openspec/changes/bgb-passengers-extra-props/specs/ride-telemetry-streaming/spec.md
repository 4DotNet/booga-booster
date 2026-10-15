# Spec Delta

## MODIFIED Requirements

### Requirement: Full telemetry payload

Each streamed frame SHALL be a complete ride snapshot, not a partial delta. It SHALL include, per the `RideTelemetry` contract: the ride lifecycle state; the total count of boarded passengers currently on the ride; the mill's commanded power, sensed RPM, total and passenger load, imbalance and balance/overload flags; each of the four hubs' power, RPM and load; each of the sixteen gondolas' brake state, yaw angle, RPM, forward and lateral G, load, and per-seat weight, restraint state, whether the seat is occupied, and whether its restraint is secured; and a rider-experience summary giving the average happiness, average preferred ride intensity and average nausea of all boarded passengers, with each average absent when no passenger is boarded.

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

- **WHEN** a telemetry frame is emitted while two passengers are boarded with happiness 70 and 90, preferred intensity 50 and 100, and nausea 0 and 20
- **THEN** the frame's rider-experience summary reports average happiness 80, average preferred intensity 75 and average nausea 10

#### Scenario: Rider-experience summary on an empty ride

- **WHEN** a telemetry frame is emitted while no passengers are boarded
- **THEN** the rider-experience summary reports no averages rather than zeros
