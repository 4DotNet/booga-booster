## ADDED Requirements

### Requirement: Mill rotation-speed limits

The system SHALL classify the central mill's rotation speed against two limits: a warn limit of 15 rpm and an unsafe (safety) limit of 18 rpm. Classification SHALL use the absolute rotation speed so the limits apply equally in either direction of rotation.

#### Scenario: Mill below the warn limit is safe
- **WHEN** the mill rotates at 15 rpm or slower
- **THEN** the mill's stress classification is `safe`

#### Scenario: Mill above the warn limit but below the safety limit warns
- **WHEN** the mill rotates faster than 15 rpm but at 18 rpm or slower
- **THEN** the mill's stress classification is `warning`

#### Scenario: Mill above the safety limit fails
- **WHEN** the mill rotates faster than 18 rpm
- **THEN** the mill's stress classification is `failure`

### Requirement: Hub rotation-speed limits

The system SHALL classify each hub's rotation speed against two limits: a warn limit of 26 rpm and an unsafe (safety) limit of 32 rpm. Because a hub's arm is shorter than the mill's, these limits are higher than the mill's. Classification SHALL use the absolute rotation speed.

#### Scenario: Hub below the warn limit is safe
- **WHEN** a hub rotates at 26 rpm or slower
- **THEN** that hub's stress classification is `safe`

#### Scenario: Hub above the warn limit but below the safety limit warns
- **WHEN** a hub rotates faster than 26 rpm but at 32 rpm or slower
- **THEN** that hub's stress classification is `warning`

#### Scenario: Hub above the safety limit fails
- **WHEN** a hub rotates faster than 32 rpm
- **THEN** that hub's stress classification is `failure`

### Requirement: Ride stress roll-up

The system SHALL expose a single ride-wide stress reading that is the worst classification across the mill and all four hubs, where `failure` is worse than `warning` and `warning` is worse than `safe`.

#### Scenario: All components safe rolls up to safe
- **WHEN** the mill and every hub are classified `safe`
- **THEN** the ride-wide stress reading is `safe`

#### Scenario: Any component warning rolls up to at least warning
- **WHEN** at least one component is classified `warning` and none is `failure`
- **THEN** the ride-wide stress reading is `warning`

#### Scenario: Any component failure rolls up to failure
- **WHEN** at least one component is classified `failure`
- **THEN** the ride-wide stress reading is `failure`

### Requirement: Automatic safety-mode trip

While the ride is running, the system SHALL automatically enter safety mode the moment the ride-wide stress reaches `failure`. Entering safety mode SHALL stop the ride and apply the brakes: motor power is cut to the mill and every hub, and the gondola brakes are engaged, without any operator action. The trip SHALL reuse the existing emergency-stop entry behavior.

#### Scenario: Over-speed while running trips safety mode
- **WHEN** the ride is in the `Started` state and any component's speed crosses its safety limit (ride-wide stress becomes `failure`)
- **THEN** the ride transitions to `EmergencyStop`, mill and hub motor power is cut, and the gondola brakes are engaged

#### Scenario: No trip below the safety limit
- **WHEN** the ride is running and the ride-wide stress is `safe` or `warning`
- **THEN** the ride stays in its current running state and no automatic stop occurs

#### Scenario: Trip requires no operator command
- **WHEN** the safety limit is crossed while running
- **THEN** the transition into safety mode happens on the next simulation step with no operator-triggered transition

### Requirement: Stress reading on telemetry

The system SHALL publish the stress classification on the live telemetry: the mill's own classification on the mill telemetry, and each hub's own classification on that hub's telemetry, so consumers can render both the ride-wide roll-up and per-component warnings.

#### Scenario: Telemetry carries per-component stress
- **WHEN** a telemetry snapshot is produced
- **THEN** the mill telemetry includes the mill's stress classification and each hub telemetry includes that hub's stress classification

### Requirement: Rotation speed panel warns per value

The rotation speed panel SHALL indicate, for the mill value and each hub value, when that value is at or over its warn limit, distinguishing `warning` from `failure`. The indication SHALL NOT rely on colour alone.

#### Scenario: Mill value over its warn limit is flagged
- **WHEN** the mill rotation speed is classified `warning` or `failure`
- **THEN** the panel visibly flags the mill value with a non-colour-only indication of that level

#### Scenario: Hub value over its warn limit is flagged
- **WHEN** a hub's rotation speed is classified `warning` or `failure`
- **THEN** the panel visibly flags that hub's value with a non-colour-only indication of that level

### Requirement: Load & security panel shows stress

The Load & security panel SHALL show a Stress row reading `safe`, `warning`, or `failure`, reflecting the ride-wide stress roll-up. The row SHALL convey its level by icon and text, never by colour alone.

#### Scenario: Stress row reflects a safe ride
- **WHEN** the ride-wide stress is `safe`
- **THEN** the Load & security panel shows "Stress: safe"

#### Scenario: Stress row reflects a warning
- **WHEN** the ride-wide stress is `warning`
- **THEN** the Load & security panel shows "Stress: warning" with a warning icon and text

#### Scenario: Stress row reflects a failure
- **WHEN** the ride-wide stress is `failure`
- **THEN** the Load & security panel shows "Stress: failure" with a failure icon and text
