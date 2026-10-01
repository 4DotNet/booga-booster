# Spec Delta

## ADDED Requirements

### Requirement: Frame carries the rider experience summary

Every telemetry frame SHALL carry a rider experience summary of the people currently on the ride: their average happiness, average preferred ride intensity and average nausea, each on the 0–100 scale. When nobody is on the ride the three averages SHALL be reported as absent rather than as zero.

#### Scenario: Averages over the boarded riders

- **WHEN** two riders are on board with happiness 60 and 90, preferred intensity 50 and 100, and nausea 0 and 50
- **THEN** the frame reports average happiness 75, average preferred intensity 75 and average nausea 25

#### Scenario: Empty ride

- **WHEN** a frame is emitted while no seat is occupied
- **THEN** the frame's rider experience summary reports no averages

#### Scenario: Summary changes during the ride

- **WHEN** riders' happiness or nausea changes while the ride runs
- **THEN** subsequent frames report the updated averages
