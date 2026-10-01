# 6. Rider Mood — Happiness, Preferred G and Nausea

Every guest carries three mood values from the queue, into a seat, and out of the exit.
This doc derives the rules and justifies every constant. The constants live in
`RideParameters.cs` (on-ride rules) and in the Queue module's `Person` /
`QueueWaitDecay` (queue rules); the values are listed in
[appendix-parameters.md](appendix-parameters.md#rider-mood).

> **Scope.** Mood is *observed*, never *acted on*: a mad guest does not leave the queue
> and a sick rider does not stop the ride. Mood never feeds back into the physics, so it
> cannot disturb determinism. Like the physics, every rule is a pure function of state and
> the fixed step `dt`.

## 6.1 The three values

| Value | Range | On arrival | Changes |
|-------|-------|-----------|---------|
| Happiness | `[0, 100]` | uniform in `[65, 85]` | falls with long queue waits; rises on a well-matched ride |
| Preferred G | `[G_max/2, G_max]` = `[2.25, 4.5]` g | uniform in that range | never |
| Nausea | `[0, 100]` | `0` | rises when the ride overshoots the preference |

Happiness and nausea are **clamped** to `[0, 100]` in one place (`Passenger.GainHappiness`
/ `GainNausea`, and `QueueWaitDecay` for the queue). The preferred G range is anchored
on the ride's safety limit `G_max = MaxGForce = 4.5 g`: nobody *wants* more than the ride
is allowed to give, and the tamest guest still wants half of it. The draws come from the
queue module's seeded random source, so a fixed seed yields the same guests.

The starting happiness `[65, 85]` sits comfortably above the dashboard's *mad* threshold
(30) and leaves headroom for the ride to make guests happier.

## 6.2 Queue wait

The queue has no tick, so happiness is not stored as it decays — it is **computed on
every read** from the group's enqueue time `t₀` (taken from the injected `TimeProvider`):

```
w     = now − t₀                         // time waited
t     = max(0, w − 5 min)                // time beyond the grace period
H(w)  = max(0, H₀ − 5 × (e^(t / 5 min) − 1))
```

| Constant | Value | Why |
|----------|------:|-----|
| Grace period | 5 min | a short wait is part of the fun |
| Decay scale | 5 pts | one time constant past the grace costs `5 (e − 1) ≈ 8.6` pts — noticeable, not brutal |
| Time constant | 5 min | the loss compounds: ~8.6 pts at 10 min, ~32 at 15, ~40 at 16 |

So a typical guest turns mad between roughly 15½ min (started at 65) and 17½ min (started
at 85). When a group boards, the queue hands each member over with the decayed value
**as of that moment**; the ride never sees the starting value.

## 6.3 Felt G

Riders respond to the **total** felt load, gravity included — what a G-meter reads
(§5.1). The gondola already computes the horizontal specific-force components `ForwardG`
and `LateralG`; the seat additionally holds the rider up against gravity (1 g vertical), so

```
FeltG = √(1 + ForwardG² + LateralG²)
```

At rest this is exactly 1.0 g; a 2 g horizontal load gives `√5 ≈ 2.24` g. Both seats of
a gondola feel the same `FeltG` — the 0.45 m lateral seat offset changes the field by a
few percent, which is negligible for mood.

## 6.4 On the ride

Mood advances **inside the gondola's physics step**, right after the G-forces are
updated, with the same fixed `dt = 1/120 s`. It therefore only changes while the physics
runs (`Started`, `Stopping`, `EmergencyStop`); loading and idle time leave it untouched.
For each seated rider with preference `P`:

### Happiness — "close to what I like is fun"

```
d = |FeltG − P|
if d < B:   H += k_H × (1 − d / B) × dt
```

| Constant | Symbol | Value | Why |
|----------|--------|------:|-----|
| `HappinessGainPerSecond` | `k_H` | 2 /s | a perfectly matched 30 s ride adds ~+60 — clearly visible on the dashboard |
| `FunBand` | `B` | 1.0 g | roughly half the swing between a calm ~1 g and an intense ~3 g; beyond that the ride feels "wrong" |

The gain is a triangle: full at a perfect match, zero at `d ≥ B`. The ride never
*lowers* happiness — only the queue does.

### Nausea — "much more than I like makes me sick"

```
if FeltG − P > f × G_max:   N += r × (N + s) × dt
```

| Constant | Symbol | Value | Why |
|----------|--------|------:|-----|
| `NauseaToleranceFraction` | `f` | 0.3 | 30 % of the limit = 1.35 g of overshoot before it turns unpleasant |
| `NauseaGrowthRate` | `r` | 0.1 /s | exponential: nausea feeds on itself |
| `NauseaSeed` | `s` | 5 pts | lets nausea start from 0 (pure `rN` would stay at 0 forever) |

Solving `dN/dt = r (N + s)` from `N = 0`: `N(t) = s (e^(rt) − 1)`. Ten seconds of
overshoot gives `5 (e − 1) ≈ 8.6`; the *sick* threshold of 70 is reached at
`t = ln(15) / 0.1 ≈ 27 s` — one ride's worth of abuse. The explicit Euler step at
`dt = 1/120 s` lands within 0.01 of the continuous solution over 10 s.

### The sustained max-G penalty

Each gondola tracks how long its `FeltG` has stayed **continuously** at or above
`G_max`. When that stretch first exceeds `MaxGPenaltySeconds` = 1 s, every rider in it
gains `MaxGNauseaPenalty` = +25 nausea, **once**. The stretch resets as soon as
`FeltG < G_max`, so a new penalty needs a new stretch longer than 1 s. A short spike
(< 1 s) is shrugged off; holding the ride at its limit is punished hard.

This is a *comfort* rule, not a safety interlock: it does not stop the ride. The
over-G interlock belongs to a safety change (cf. `passenger-safety`).

## 6.5 Identity from queue to exit

A boarded rider keeps their guest number, happiness, preferred G and nausea (a
`PassengerSeed` built from the queue's view of the guest). Seat telemetry carries them,
`null` for an empty seat. A rider boarded by hand (the manual board endpoint) has no
guest number and the default mood: happiness 75 and preferred G 3.375 g, the middles of
the respective ranges.

When the ride offloads, it first captures who is leaving and how they feel into
`LastOffload { Counter, Riders }`; the counter rises by one per (non-empty) offload, so a
consumer detects a new offload by the counter changing. It is a snapshot, not an event.

## 6.6 Spike — is `G_max` reachable?

Measured by `FeltGSpikeTests` (2 minutes at fixed power, all hubs forward, peak felt G
over all 16 gondolas). The test pins these numbers; update this table if physics changes
move them.

| Mill power | Hub power | Riders | Peak felt G | First at ≥ 4.5 g |
|-----------:|----------:|-------:|------------:|-----------------:|
| 100 % | 100 % | 32 | **6.42 g** | after 13.2 s |
| 100 % | 100 % | 0 | 5.99 g | after 9.7 s |
| 75 % | 75 % | 32 | 4.89 g | after 22.8 s |
| 50 % | 50 % | 32 | 3.38 g | never |
| 100 % | 0 % | 32 | 3.67 g | never |
| 0 % | 100 % | 32 | 2.56 g | never |

**Result: yes.** With both motors at full power the rig reaches `G_max` within ~13 s and
peaks well above it, so the max-G penalty is a live rule that an aggressive operator will
trigger. The hubs spinning on top of the mill are what push it over: either motor alone
stays below the limit. (This is far above the "~2 g at terminal speed" in the
[appendix sanity check](appendix-parameters.md#sanity-check), which describes the mill
alone at a gentler cruise; the combined field of mill and hub adds up.)

Consequences:

- Thrill-seekers (preference near 4.5 g) only get close to their preference at high power
  (around 75 %, where the ride brushes the limit). At full power most guests overshoot by
  more than 1.35 g and get nauseous, and every rider takes the +25 penalty.
- If `passenger-safety`'s over-speed trip keeps the ride below 4.5 g, the penalty simply
  never fires — that is acceptable; `MaxGForce` is a safety limit and is not tuned for mood.
- The peaks are reached with the gondolas swinging freely: a gondola's own swing adds an
  `ω² r_g` term to the field (`Gondola.UpdateGForces`), so individual gondolas peak above
  the steady-state value.
