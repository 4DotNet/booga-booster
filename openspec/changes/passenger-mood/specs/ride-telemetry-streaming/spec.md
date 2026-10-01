## ADDED Requirements

### Requirement: Seat telemetry carries the rider's mood

Each seat in the telemetry payload SHALL carry the seated rider's guest number, happiness, preferred G and nausea, or nulls when the seat is empty. Each gondola SHALL additionally carry its felt G.

#### Scenario: Occupied seat reports its rider
- **WHEN** guest 42 with happiness 70, preferred G 3.1 and nausea 12 is seated
- **THEN** that seat's telemetry reports guest number 42, happiness 70, preferred G 3.1 and nausea 12

#### Scenario: Empty seat reports nulls
- **WHEN** a seat is empty
- **THEN** its guest number, happiness, preferred G and nausea are null

### Requirement: Telemetry reports the last offload

The telemetry payload SHALL include the ride's last offload: its offload counter and the list of offloaded riders with guest number, final happiness and final nausea. Before the first offload the counter SHALL be 0 and the list empty.

#### Scenario: Offload appears in telemetry
- **WHEN** the ride offloads and the next telemetry frame is emitted
- **THEN** the frame's last offload counter has increased by one and lists the offloaded riders

#### Scenario: No offload yet
- **WHEN** the ride has never offloaded
- **THEN** the last offload counter is 0 and its rider list is empty
