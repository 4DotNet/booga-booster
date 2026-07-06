## ADDED Requirements

### Requirement: Telemetry stream endpoint

The DigitalTwin module SHALL expose an HTTP endpoint that returns a Server-Sent Events stream (`Content-Type: text/event-stream`) of ride telemetry. The connection SHALL be accepted and held open regardless of the ride's current state, and SHALL remain open until the client disconnects. Each emitted event SHALL be a full `RideTelemetry` snapshot serialized as JSON.

#### Scenario: Client opens the stream

- **WHEN** a client sends `GET` to the telemetry stream endpoint
- **THEN** the server responds with `200 OK` and `Content-Type: text/event-stream`
- **AND** the connection is kept open for streaming

#### Scenario: Client disconnect ends the stream

- **WHEN** a connected client closes the connection
- **THEN** the server stops producing frames for that client and releases its resources

### Requirement: Full telemetry payload

Each streamed frame SHALL be a complete ride snapshot, not a partial delta. It SHALL include, per the `RideTelemetry` contract: the ride lifecycle state; the mill's commanded power, sensed RPM, total and passenger load, imbalance and balance/overload flags; each of the four hubs' power, RPM and load; and each of the sixteen gondolas' brake state, yaw angle, RPM, forward and lateral G, load, and per-seat weight and restraint state.

#### Scenario: Frame carries mill and hub engine readings

- **WHEN** a telemetry frame is emitted while the ride is running
- **THEN** it contains the mill's power and RPM
- **AND** it contains the power and RPM of all four hubs

#### Scenario: Frame carries cart and seat state

- **WHEN** a telemetry frame is emitted while the ride is running
- **THEN** each gondola's brake, angle, RPM, forward/lateral G and load are present
- **AND** each seat's measured weight and restraint state are present

### Requirement: Fixed sampling rate decoupled from physics

The stream SHALL sample the ride and emit frames at a fixed telemetry rate that is independent of the physics timestep. The telemetry rate SHALL be configurable and SHALL default to a value suitable for smooth animation (e.g. 30 Hz). Sampling SHALL read a consistent snapshot of the ride's current state and SHALL NOT drive or block the physics loop.

#### Scenario: Frames arrive at the telemetry rate

- **WHEN** the ride is running and a client is connected
- **THEN** frames are emitted at approximately the configured telemetry rate
- **AND** the emission rate does not change if the physics timestep changes

### Requirement: Emission gated on the running ride

The stream SHALL emit telemetry frames only while the ride is in its running state. While the ride is not running, the connection SHALL stay open but no telemetry frames SHALL be emitted. When the ride enters the running state, frames SHALL begin; when it leaves the running state, frames SHALL cease.

#### Scenario: No frames while the ride is idle

- **WHEN** a client is connected and the ride is not running
- **THEN** no telemetry frames are emitted
- **AND** the connection remains open

#### Scenario: Frames begin when the ride starts

- **WHEN** a client is connected and the ride transitions into the running state
- **THEN** telemetry frames begin to be emitted at the telemetry rate

#### Scenario: Frames stop when the ride stops running

- **WHEN** the ride leaves the running state while a client is connected
- **THEN** telemetry frames cease
- **AND** the connection remains open for a later run

### Requirement: Frontend maintains a persistent stream connection

The frontend SHALL open the telemetry stream connection when the dashboard loads, independent of the ride's state, and SHALL keep it open for the lifetime of the dashboard. If the connection drops, the frontend SHALL automatically attempt to reconnect. The connection SHALL be closed when the dashboard is destroyed.

#### Scenario: Connection opens on dashboard load

- **WHEN** the ride dashboard is initialized
- **THEN** the frontend opens the SSE telemetry connection even though the ride may not be running

#### Scenario: Automatic reconnect after a drop

- **WHEN** an open telemetry connection is dropped
- **THEN** the frontend automatically attempts to re-establish it without operator action

#### Scenario: Connection closes on teardown

- **WHEN** the dashboard is destroyed
- **THEN** the frontend closes the telemetry connection

### Requirement: Incoming frames update a single shared store

Each received telemetry frame SHALL update one local telemetry store; all dashboard panels SHALL read their values as signals from that store and SHALL NOT connect to the stream individually. Before the first frame arrives (or while the ride is not running), the store SHALL expose a well-defined at-rest/idle snapshot so panels render without errors.

#### Scenario: Panels reflect a new frame

- **WHEN** a telemetry frame arrives
- **THEN** the shared store updates and every panel bound to the affected signals re-renders with the new values

#### Scenario: Store has a defined value before the first frame

- **WHEN** the dashboard has loaded but no telemetry frame has yet arrived
- **THEN** the store exposes an at-rest snapshot and panels render without error

### Requirement: SSE source replaces the simulator and routes commands over HTTP

The SSE-backed telemetry source SHALL become the application's active telemetry source in place of the client-side simulator. Operator commands (engine power, direction, gondola brake) SHALL be sent to the backend HTTP command endpoints rather than applied to a local simulator. The read model exposed to panels SHALL be sourced solely from streamed frames.

#### Scenario: Dashboard runs on backend telemetry

- **WHEN** the app is configured for normal operation
- **THEN** the active telemetry source is the SSE stream, not the simulator
- **AND** the panels display values originating from the backend

#### Scenario: Operator command is sent to the backend

- **WHEN** the operator changes engine power on the dashboard
- **THEN** the change is POSTed to the backend command endpoint
- **AND** the resulting effect is observed on a subsequent streamed frame
