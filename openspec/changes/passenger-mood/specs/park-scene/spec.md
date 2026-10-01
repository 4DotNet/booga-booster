## ADDED Requirements

### Requirement: Entry and exit booths

The central visualization SHALL show an entry booth and an exit booth on the ground outside the ride's sweep, styled after RollerCoaster Tycoon ride entrances: a small box building with a striped roof, the entry and exit visually distinguishable from each other.

#### Scenario: Booths are placed clear of the ride
- **WHEN** the visualization renders
- **THEN** an entry booth and an exit booth stand on the ground beyond the gondolas' outer sweep radius, and no rotating part of the ride passes through them

### Requirement: Guests are drawn as instanced figures

Every guest drawn in the scene SHALL be a figure made of a pill-shaped body in a shirt colour and a sphere head. All bodies SHALL share one instanced mesh and all heads another. A guest's shirt colour SHALL be chosen from a fixed palette by their guest number so it stays the same across updates.

#### Scenario: Shirt colour is stable
- **WHEN** the same guest appears in two consecutive queue updates
- **THEN** their shirt colour is identical in both

### Requirement: Head colour shows mood

A guest's head SHALL be green when their nausea is 70 or more, otherwise red when their happiness is below 30, otherwise LEGO yellow.

#### Scenario: Mad guest has a red head
- **WHEN** a queued guest's happiness is 25 and nausea is 0
- **THEN** their head is red

#### Scenario: Sick wins over mad
- **WHEN** a rider has happiness 20 and nausea 80
- **THEN** their head is green

#### Scenario: Content guest has a yellow head
- **WHEN** a guest has happiness 60 and nausea 10
- **THEN** their head is LEGO yellow

### Requirement: The queue is visible with overflow

The scene SHALL draw the first 60 queued guests in queue order along a fixed switchback path that ends at the entry booth, front of the queue nearest the booth. When more than 60 guests are queued, a label `+N` SHALL be shown at the far end of the path, where N is the number of guests not drawn.

#### Scenario: Short queue is fully drawn
- **WHEN** 12 guests are queued
- **THEN** 12 figures stand on the queue path and no overflow label is shown

#### Scenario: Long queue overflows
- **WHEN** 87 guests are queued
- **THEN** 60 figures stand on the queue path and the label reads `+27`

### Requirement: Guests walk between queue updates

On each queue update, guests SHALL walk at a constant walking speed from their current position to their new slot. Newly queued guests SHALL appear at the far end of the path; guests who left the queue to board SHALL walk to the entry booth and disappear. Under `prefers-reduced-motion` guests SHALL appear at their target position immediately.

#### Scenario: Queue advances
- **WHEN** the front group boards and the queue update arrives
- **THEN** the remaining guests walk forward to their new slots and the boarded guests walk into the entry booth and disappear

#### Scenario: Reduced motion places guests instantly
- **WHEN** the user prefers reduced motion and a queue update arrives
- **THEN** every guest is drawn at their new slot without walking

### Requirement: Seated riders ride along

Every occupied seat SHALL show a figure seated in its gondola that moves with the gondola, with its head coloured by that rider's mood from the telemetry stream.

#### Scenario: Rider turns green during the ride
- **WHEN** a seated rider's nausea rises to 70 or more
- **THEN** their figure's head in the gondola turns green

### Requirement: Offloaded riders exit, and sick riders puke

When the telemetry's offload counter increases, each offloaded rider SHALL appear at the exit booth and walk away along a short exit path until they leave the scene. A rider with nausea of 70 or more SHALL leave a green puddle on the ground near the exit as they walk out. A puddle SHALL shrink away over 20 seconds and then be removed. This effect is purely visual and has no backend state.

#### Scenario: Sick rider leaves a puddle
- **WHEN** an offload contains a rider with nausea 85
- **THEN** that rider walks out of the exit and a green puddle appears near the exit, which shrinks and disappears within 20 seconds

#### Scenario: Healthy riders leave no puddle
- **WHEN** an offload contains only riders with nausea below 70
- **THEN** riders walk out of the exit and no puddle appears

### Requirement: The scene stays decorative

The visualization SHALL remain `aria-hidden`. Every piece of information it adds (mood, sick riders, overflow) SHALL also be available as text in the Rider Experience panel or the queue panel.

#### Scenario: No information only in the scene
- **WHEN** a screen reader user inspects the dashboard
- **THEN** the counts of mad guests, sick riders and riders who left sick, and the queue length, are available as text
