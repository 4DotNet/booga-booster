## ADDED Requirements

### Requirement: The mill coasts to rest in a bounded, realistic time

When the mill's drive power is cut, the mill SHALL decelerate under its friction and aerodynamic losses and reach a complete rest (`IsAtRest`) within a realistic bounded time, rather than creeping at low speed for minutes. The tuned loss coefficients MUST bring both an empty rig and a fully loaded rig to rest from cruise within the target coast-down window, and MUST NOT stop the rig so abruptly that the coast-down reads as an instant hard brake.

The target coast-down window (from cruise, i.e. steady state on full power, to `IsAtRest`) is **8–45 seconds for an empty mill** and **up to 55 seconds for a fully loaded mill** — a fully loaded rig carries ~2.5× the empty rotating inertia and so legitimately coasts longer under the same losses, so its ceiling is relaxed accordingly rather than forcing losses so high they gut the powered cruise speed.

#### Scenario: Empty mill settles within the window

- **WHEN** an empty mill is run to its steady cruise speed and its power is then set to `Off`
- **THEN** the mill reaches `IsAtRest` (|ω| below the rest threshold) no later than 45 simulated seconds after power is cut
- **AND** it does not reach `IsAtRest` in under 8 simulated seconds

#### Scenario: Fully loaded mill settles within the window

- **WHEN** every hub is filled to the maximum passenger mass, the mill is run to its steady cruise speed, and its power is then set to `Off`
- **THEN** the mill reaches `IsAtRest` no later than 55 simulated seconds after power is cut

### Requirement: The hubs also settle promptly when power is cut

When drive power is cut, each hub SHALL decelerate under its own retuned losses and reach `IsAtRest` without a long low-speed tail, so that the whole rig (mill and all four hubs) is reported at rest promptly rather than a hub spinning on after the mill has effectively stopped.

#### Scenario: A coasting hub reaches rest

- **WHEN** a hub is spun up on full power and its power is then set to `Off`
- **THEN** the hub reaches `IsAtRest` within the mill's coast-down window under the same conditions

### Requirement: Retuning the losses preserves energy honesty and the powered speed range

Raising the loss coefficients SHALL NOT break the existing rotational-dynamics guarantees: coast-down kinetic energy MUST remain monotonically non-increasing (losses only remove energy), and a powered mill MUST still spin up to a cruise speed that stays within the existing over-speed cap and close enough to it to keep the ride's felt G within its intended range.

#### Scenario: Kinetic energy never rises while coasting

- **WHEN** the mill is coasting down with power off, over the whole coast-down
- **THEN** its total rotational kinetic energy at each step is less than or equal to the previous step's

#### Scenario: The rig still reaches cruise under power

- **WHEN** the mill is run on full power for a sustained period
- **THEN** it accelerates to a cruise speed that is at or below the over-speed cap and remains a substantial fraction of that cap (not driven so low by the added losses that the ride loses its intended speed and felt G)
