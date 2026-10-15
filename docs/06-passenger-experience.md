# 6. Passenger Experience — Happiness, Preferred Intensity and Nausea

The first five documents describe what the ride *does*. This one describes what the ride
does *to people*: every guest carries a mood, a taste for intensity and a stomach, and the
twin evolves all three from the same felt G-forces doc 5 derives. It is the specification
the experience code and its tests are written against (OpenSpec change
`bgb-passengers-extra-props`, design D1–D7).

---

## 6.1 The three attributes

Every guest — queued or seated — carries three numbers on a common **0–100 scale**
(`ExperienceScaleMin` = 0, `ExperienceScaleMax` = 100):

| Attribute | 0 means | 100 means |
|-----------|---------|-----------|
| Happiness `H` | very sad | extremely happy |
| Preferred ride intensity `P` | very tame | the maximum allowed G-force |
| Nausea `N` | fine | extremely nauseous |

Creating a guest with a value outside the scale is a domain validation error. The
*dynamics* below never throw: whatever a rule computes is **clamped** to `[0, 100]`,
because a continuous process that overshoots by a hair is not an invalid request.

### Starting values (Queue module)

A guest generated on arrival draws `H ~ Uniform[65, 85]` and `P ~ Uniform[50, 100]` from the
seeded person generator, and starts with `N = 0`. The same seed yields the same guests.

A passenger boarded directly into a seat (the operator's single-seat boarding command,
which has no queue history) starts from the midpoints of those arrival bands:
`DefaultRiderHappiness` = 75, `DefaultRiderPreferredIntensity` = 75, nausea 0.

---

## 6.2 Waiting: exponential happiness decay (Queue module)

The queue records when each group joined it. A queued guest's current happiness is a pure
function of their arrival happiness `H₀` and the time they have waited `w`:

```
H(w) = H₀                                for w ≤ grace
H(w) = H₀ · 2^(−(w − grace) / halfLife)  for w > grace
```

with `grace` = 5 min and `halfLife` = 10 min by default (`QueueModuleOptions.HappinessGracePeriod`
/ `HappinessHalfLife`). A guest who arrived at 80 is still at 80 after 4 minutes, has
dropped to 40 after 15 minutes and to 20 after 25.

The function is evaluated **lazily** — when the queue status is read, and when a group is
taken for boarding — from the enqueue time and the module's `TimeProvider`. There is no
background ticker mutating anyone, so a reading is exact at any instant and trivially
testable with a fake clock. The value at the moment a group leaves the line is the
happiness its members board with; their preference and nausea travel with them unchanged.

---

## 6.3 Ride intensity — felt G as a share of max G

A gondola's **felt G** is the magnitude of the two horizontal specific-force components the
gondola already computes and streams each tick (doc 5 §5.1, `ForwardG` and `LateralG`):

```
G_felt    = √(ForwardG² + LateralG²)
intensity = clamp(100 · G_felt / G_max, 0, 100)        G_max = MaxGForce = 4.5 g
```

So intensity 50 is 2.25 g and intensity 100 is 4.5 g; anything above `G_max` still reads 100.
The preference scale is tied to "% of maximum allowed G" literally, so `P = 50` is a rider
who is happiest at 2.25 g.

> **Why horizontal only.** The constant 1 g of gravity is felt equally on every ride and
> every seat; it says nothing about how wild *this* ride is. Using the streamed components
> keeps the measure identical to what the operator sees on the dashboard.

**Consequence worth knowing.** At the baseline parameters the ride reaches ~2 g at terminal
speed (appendix sanity check) and the doc 5 worked example peaks at ~2.8 g, i.e. intensity
≈ 45–62. Riders with a high preference will seldom hit their match band, and over-G episodes
(§6.6) are rare. That follows the request as written; the whole scale hangs off the single
constant `MaxGForce`, so re-basing it is a one-line change.

---

## 6.4 Riding: happiness rises when the intensity matches

Each physics tick, for every seated passenger, let `Δ = intensity − P`. When the ride is
within the **match band** of the passenger's preference:

```
if |Δ| ≤ B:   H += r · (1 − |Δ| / B) · dt          B = HappinessMatchBand = 15 points
                                                   r = HappinessGainRate  = 2 points/s
```

The gain is a triangular kernel: full rate `r` at an exact match, falling linearly to zero at
the edge of the band. Outside the band intensity does not touch happiness at all.

*Sizing.* At an exact match a typical arriving guest (`H ≈ 75`) reaches 100 in
`25 / 2 = 12.5 s` — a satisfying rise within one ride, slow enough to see on a 30 Hz
dashboard. A 10-point mismatch gains a third of that rate.

---

## 6.5 Riding: nausea grows exponentially when the ride is too intense

When the ride exceeds the passenger's preference by at least the **excess threshold**:

```
if Δ ≥ E:   N += k · (N + s) · dt                  E = NauseaIntensityExcess = 30 points
                                                   k = NauseaGrowthRate      = 0.15 /s
                                                   s = NauseaSeed            = 1 point
```

This is the explicit-Euler step of `dN/dt = k (N + s)`, whose solution is

```
N(t) = (N₀ + s) · e^{k t} − s
```

The seed `s` lets growth start from `N₀ = 0` (pure `dN/dt = kN` would stay at zero for ever),
and makes each equal interval add more nausea than the one before. From 0, nausea reaches
100 after `ln(101) / 0.15 ≈ 31 s` of sustained over-intensity. Per tick the growth factor is
`1 + k·dt = 1.00125`; over one second that compounds to `1.1618`, matching `e^{0.15} = 1.1618`
to four digits, so the discrete step is a faithful integration at `dt = 1/120 s`.

Below the threshold intensity adds no nausea. Nausea never decays and does not feed back into
happiness (non-goals of this change).

---

## 6.6 Over-G episodes

A gondola that holds `G_felt ≥ G_max` for **more than** `OverGNauseaDwell` (1 s) gives every
passenger in it `OverGNauseaPenalty` (25) nausea points — once per episode:

```
if G_felt ≥ G_max:
    overG += dt
    if overG > dwell and not triggered:
        each passenger: N += penalty      (clamped)
        triggered = true
else:
    overG = 0 ; triggered = false          // the episode is over
```

A 3-second episode costs exactly 25 points; a 0.5-second blip costs nothing; two episodes
separated by a dip below `G_max` cost 50. "Gain 25 % nausea" is read as +25 points on the
0–100 scale — a multiplicative 25 % of a nausea of 0 would do nothing. Each penalty is also
counted on the `boogabooster.ride.over_g.nausea_episodes` metric.

Over-G here only affects nausea. Reducing power above `G_max` is the safety interlock's job
(doc 1 §1.5, the `passenger-safety` change), not this model's.

---

## 6.7 Where it runs in the tick, and determinism

The on-ride rules run inside `Gondola.AdvancePhysics`, straight after the gondola's G-forces
are updated (doc 1 §1.4 step 4), for every occupied seat:

```
3. Passive carts …
4. Derived telemetry   G-forces …
   4a. Rider experience  intensity → happiness gain, nausea growth, over-G episode
```

They are pure arithmetic on state at the fixed `dt`: no clock, no randomness. Two runs with
the same seed and the same operator inputs produce bit-identical happiness and nausea for
every passenger at every tick. Updating at the 30 Hz telemetry rate instead was rejected: it
would tie domain behaviour to the presentation rate. The per-tick cost — at most 32 riders ×
a handful of multiplications, on a value type with no allocation — is negligible against the
8.3 ms tick budget.

### Telemetry

Each telemetry frame carries a summary of everyone on board — rider count and the average
happiness, preferred intensity and nausea — with the averages absent (not 0) when the ride is
empty. Per-seat experience is deliberately not streamed.

---

## 6.8 Parameters

| Parameter | Constant | Default | Unit |
|-----------|----------|--------:|------|
| Experience scale | `ExperienceScaleMin` / `ExperienceScaleMax` | 0 / 100 | points |
| Default rider happiness (seat boarding) | `DefaultRiderHappiness` | 75 | points |
| Default rider preference (seat boarding) | `DefaultRiderPreferredIntensity` | 75 | points |
| Happiness match band | `HappinessMatchBand` | 15 | points |
| Happiness gain rate (exact match) | `HappinessGainRate` | 2 | points/s |
| Nausea intensity excess | `NauseaIntensityExcess` | 30 | points |
| Nausea growth rate | `NauseaGrowthRate` | 0.15 | 1/s |
| Nausea seed | `NauseaSeed` | 1 | points |
| Over-G dwell | `OverGNauseaDwell` | 1 | s |
| Over-G penalty | `OverGNauseaPenalty` | 25 | points |
| Queue grace period | `QueueModuleOptions.HappinessGracePeriod` | 5 | min |
| Queue happiness half-life | `QueueModuleOptions.HappinessHalfLife` | 10 | min |

All DigitalTwin constants live in `RideParameters`; the queue values are bound options.

---

*Back to the [index](README.md). Default numeric parameters:
[appendix-parameters.md](appendix-parameters.md).*
