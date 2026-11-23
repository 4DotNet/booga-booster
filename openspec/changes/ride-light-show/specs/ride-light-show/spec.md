## Purpose

Defines the decorative light show of the ride visualization: the twelve bulb patterns,
how they are sequenced, the blinking and fading pole spotlights, and the limits that
keep the show safe to watch.

## ADDED Requirements

### Requirement: Twelve bulb patterns

The light show SHALL provide exactly 12 distinct bulb animation patterns. Each pattern
SHALL have a fixed loop duration and SHALL be a pure function of the bulb and the time
within its loop, so the same bulb at the same loop time always shows the same
brightness. Every brightness a pattern produces SHALL lie between 0 (off) and 1 (full).

#### Scenario: Pattern catalogue

- **WHEN** the light show is inspected
- **THEN** it offers 12 patterns, each with a unique name and a positive loop duration

#### Scenario: Deterministic output

- **WHEN** a pattern is evaluated twice for the same bulb and loop time
- **THEN** both evaluations return the same brightness

#### Scenario: Brightness range

- **WHEN** any pattern is evaluated for any bulb at any loop time
- **THEN** the brightness is at least 0 and at most 1

### Requirement: Pattern sequencing

The light show SHALL start with the first pattern when the visualization opens, play
each pattern for 5 consecutive loops, then switch to the next pattern, and after the
twelfth pattern start again at the first, repeating indefinitely.

#### Scenario: Show starts on open

- **WHEN** the visualization has just opened
- **THEN** the first pattern is playing, in its first loop

#### Scenario: Five loops then the next pattern

- **WHEN** the current pattern has completed its fifth loop
- **THEN** the next pattern starts at the beginning of its first loop

#### Scenario: Cycle wraps around

- **WHEN** the twelfth pattern has completed its fifth loop
- **THEN** the first pattern starts again

### Requirement: Every decorative bulb takes part

All decorative bulbs of the ride and its scenery — main arms, hub arms, hubs, the
mill crown, the base, gondola rims, the backdrop and the light poles — SHALL be driven
by the light show, each keeping its designed colour (warm white, pink or cyan) unless
the playing pattern recolours it.

#### Scenario: Bulbs on moving parts follow the rig

- **WHEN** the mill and hubs are turning
- **THEN** the bulbs on the arms, hubs and gondolas move with them while still showing the current pattern

### Requirement: Pole spotlights blink and fade

Each light pole SHALL carry a narrow-beam spotlight in a theme colour (pink, cyan,
gold or purple) aimed at the ride, lighting the ride and the backdrop behind it. The
spotlights SHALL animate with a mix of smooth fades and blinks, phase-shifted per pole
so they do not all change together.

#### Scenario: Spots animate

- **WHEN** the light show is running
- **THEN** the spotlight brightness of each pole changes over time between dim and full, with fades and blinks

#### Scenario: Spots are out of step

- **WHEN** two different poles are compared at the same moment
- **THEN** their spotlight brightness is generally different

### Requirement: Safe flash rate

No part of the light show — bulb patterns or spotlights — SHALL flash more than 3 times
in any one-second period.

#### Scenario: Fastest blink

- **WHEN** the fastest blinking pattern or spotlight effect is playing
- **THEN** it produces at most 3 off-to-on flashes per second

### Requirement: Reduced motion

When the operator's system requests reduced motion, the light show SHALL NOT animate:
bulbs and spotlights SHALL hold a steady glow.

#### Scenario: Reduced motion requested

- **WHEN** the visualization opens with `prefers-reduced-motion: reduce`
- **THEN** every bulb and spotlight holds a constant brightness and no pattern sequence runs
