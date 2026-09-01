# weather-events Specification

## Purpose

Defines the user-triggerable weather events - precipitation and strong wind - their effects and their durations.

## Requirements

### Requirement: User can trigger precipitation

A user SHALL be able to trigger a precipitation event of a chosen type — rain, snow, or hail. Triggering SHALL set the active regime to `Precipitation` with the chosen type and begin the event immediately.

#### Scenario: Rain is triggered

- **WHEN** a user triggers precipitation of type rain
- **THEN** the active regime becomes `Precipitation`, the current precipitation type is rain, and the event's duration begins counting down

#### Scenario: Snow and hail are supported

- **WHEN** a user triggers precipitation of type snow or hail
- **THEN** the active regime becomes `Precipitation` with the corresponding type

### Requirement: Precipitation effects

While a precipitation event is active, the weather SHALL trend toward cooler-than-default temperature and somewhat-higher-than-default wind, while the chosen precipitation type is in effect. These effects SHALL be applied gradually through the normal per-tick drift toward the regime's target, not instantaneously.

#### Scenario: Temperature drops and wind picks up during precipitation

- **WHEN** a precipitation event has been active for several ticks
- **THEN** the temperature is lower than the moderate default and the wind is higher than the light default

### Requirement: Precipitation lasts about fifteen minutes then ends

A precipitation event SHALL last approximately 15 minutes of simulation time. When that duration elapses, the event SHALL stop and the active regime SHALL return to `Calm`, after which the weather gradually reverts to the defaults.

#### Scenario: Precipitation stops after its duration

- **WHEN** approximately 15 minutes of simulation time have elapsed since precipitation was triggered
- **THEN** precipitation stops, the precipitation type returns to none, and the active regime returns to `Calm`

#### Scenario: Weather reverts to defaults after precipitation ends

- **WHEN** a precipitation event has ended and the simulation continues to tick
- **THEN** temperature and wind gradually return toward their moderate defaults

### Requirement: User can trigger strong winds

A user SHALL be able to trigger a strong-wind event. Triggering SHALL set the active regime to `StrongWind` and begin the event immediately.

#### Scenario: Strong wind is triggered

- **WHEN** a user triggers a strong-wind event
- **THEN** the active regime becomes `StrongWind` and the event's duration begins counting down

### Requirement: Strong-wind effects

While a strong-wind event is active, the weather SHALL trend toward much-higher-than-default wind (very strong winds), reduced sunshine, and cooler-than-default temperature, applied gradually through the normal per-tick drift toward the regime's target.

#### Scenario: Wind climbs, sunshine fades, temperature drops during strong wind

- **WHEN** a strong-wind event has been active for several ticks
- **THEN** the wind is much higher than the light default, the sunshine is lower than the default, and the temperature is lower than the moderate default

### Requirement: Strong wind lasts up to about ten minutes then eases

A strong-wind event SHALL last up to approximately 10 minutes of simulation time. When that duration elapses, the event SHALL stop and the active regime SHALL return to `Calm`, after which wind gradually eases and sunshine and temperature gradually return to their defaults.

#### Scenario: Strong wind stops after its duration

- **WHEN** approximately 10 minutes of simulation time have elapsed since strong wind was triggered
- **THEN** the strong-wind event stops and the active regime returns to `Calm`

#### Scenario: Conditions revert to defaults after strong wind ends

- **WHEN** a strong-wind event has ended and the simulation continues to tick
- **THEN** wind gradually decreases toward the light default and sunshine and temperature gradually return toward their defaults
