# 6. Rider Experience — Preference, Happiness and Nausea

The physics of docs 1–5 ends in a number: the G-force at each seat. This document turns
that number into the thing the ride exists for — how the **riders feel**. Every guest
carries a **preferred intensity**, a **happiness** and a **nausea** rating; the ride's
felt G moves the last two every tick, and a long wait in the queue erodes the middle
one before anyone sits down. Like the rest of the physics, the model is a pure,
deterministic function of state and `dt`: no randomness, no wall clock.

Every constant below lives in `RideParameters` (DigitalTwin) or `QueueModuleOptions`
(Queue) and is tabulated in [appendix-parameters.md](appendix-parameters.md). The
numbers are **tuning values, not derivations** — educated guesses chosen to give
readable behaviour over a two-to-three-minute ride cycle. Retune there, and nowhere else.

---

## 6.1 Intensity — the felt G relative to the limit

A rider does not feel throttle or rpm; they feel **specific force** (doc 5 §5.1). Two
gondolas on the same ride can feel very different G because of the beat (doc 5 §5.3), so
intensity is a property of the **gondola**, computed where its lateral and forward G
already are, every physics tick:

```
G_h       = √(G_lateral² + G_forward²)          // felt horizontal G, in g
intensity = clamp(G_h / G_max, 0, 1)            // G_max = 4.5 g (doc 5 §5.7)
atLimit   = G_h ≥ G_max
```

Why **horizontal only**: gravity's constant 1 g would put a stationary ride's intensity
floor at `1 / 4.5 ≈ 0.22`. What the ride *adds* to a rider is the horizontal specific
force, so that is what intensity measures — a ride at rest is intensity `0` by construction.

Why **relative to `G_max`**: dividing by the maximum allowed G puts intensity on the same
`[0, 1]` scale as a rider's preference, so the two can be compared directly.

### Sanity checks

```
at rest                      ⇒ G_h = 0      ⇒ intensity = 0,   atLimit = false
G_h = 2.25 g (half the max)  ⇒ intensity = 0.5
G_h = 5 g (over the max)     ⇒ intensity = 1 (saturated), atLimit = true
```

At full power the horizontal G peaks around 2–2.8 g (doc 5 §5.3, appendix sanity check),
so intensity in normal operation sits mostly in `0.4–0.6` and `atLimit` is rare — the
limit path (§6.3) is there for abuse, the continuous path (§6.2) does the everyday work.

---

## 6.2 Mood dynamics — matched, too intense, or tamer

Each seated passenger carries an immutable **preferred intensity** `p ∈ [0.1, 1]` (a
preference of `0` would match a standstill, so the floor is `0.1`) and two mutable moods,
**happiness** `H` and **nausea** `N`, both clamped to `[0, 1]`. After the gondola updates
its G-forces, every seated passenger experiences the tick:

```
Δ   = intensity − p
tol = 0.1                                       // IntensityMatchTolerance

|Δ| ≤ tol   (matched)      : H += 0.1 · dt      // HappinessGainPerSecond
Δ   > tol   (too intense)  : H −= 0.1 · dt      // HappinessLossPerSecond
                             N += 0.2 · dt      // NauseaGainPerSecond
Δ   < −tol  (tamer)        : no change          // boredom is not modelled
H, N ← clamp(·, 0, 1)
```

The tolerance is **symmetric**: a rider who prefers `0.6` is equally content at `0.5`
and `0.7`, and `0.75` is already too much. Nausea grows twice as fast as happiness falls,
so an overwhelmed rider's nausea is the first number to look alarming on the dashboard.

### Worked examples

```
p = 0.6, H = 0.7, two seconds at intensity 0.6   ⇒ H = 0.7 + 0.1·2 = 0.9
p = 0.2, H = 0.8, N = 0, two seconds at 1.0      ⇒ H = 0.8 − 0.1·2 = 0.6,  N = 0 + 0.2·2 = 0.4
p = 0.9, two seconds at intensity 0.3            ⇒ unchanged
```

### Mood is frozen while the ride is not in motion

`Ride.Advance` steps the physics — and therefore the experience — only while the ride
is `Started`, `Stopping` or `EmergencyStop`. While `Loading`, `Safe` or `Offloading` the
gondolas never tick, so a rider sitting still at 1 g feels nothing and their mood is
exactly what they boarded with. This is a **hard test**: a rider whose preference matches
a standstill (`p = 0.1`, intensity `0`) *would* gain happiness every tick if the
experience ran; ten seconds of loading must leave them unchanged.

### Mood is measured in simulated time

`dt` is the fixed `1/120 s` physics step (doc 1 §1.1). Because `RideParameters.TimeStep`
is a `TimeSpan` quantised to 100 ns, 240 steps are `1.999992 s` rather than exactly two
seconds; a test that counts ticks must expect *rate × elapsed simulated time*, not the
round number. The rates themselves are per second of ride, not per tick, so a different
step size gives the same result over the same simulated interval.

---

## 6.3 Sustained G — a one-off nausea penalty, latched per gondola

Two seconds pinned at the maximum allowed G gets to everyone, thrill seeker or not. The
latch is a property of the **gondola** — G is — so there is one counter per gondola, not
one per rider:

```
if atLimit:
    t_limit += dt
    if not fired and t_limit ≥ 2 s:             // SustainedGLimitSeconds
        for each seated rider: N += 0.5          // SustainedGLimitNauseaPenalty (clamped to 1)
        fired = true
else:
    t_limit = 0
    fired   = false                              // dropping below the limit re-arms it
```

- **Once per episode.** Five seconds at the limit is one penalty, not two and a half.
- **Continuous, not cumulative.** One second at the limit, a dip below, and another
  second is two half-episodes — no penalty.
- **Re-armed by dropping below.** Two seconds, a dip, two more seconds is two penalties.
- **No exemption.** A `p = 1` rider is *matched* at the limit and gains happiness the
  whole time — and still takes the `0.5`.

The penalty rides on top of §6.2: at intensity `1` a `p = 0.2` rider is also losing
happiness and gaining `0.2/s` of nausea while the latch counts.

---

## 6.4 Where a profile comes from

| Path | Preferred intensity | Happiness | Nausea |
|------|--------------------:|----------:|-------:|
| Arriving in the queue (`PersonGenerator`, seeded) | uniform `[0.1, 1]` | uniform `[0.65, 0.85]` | `0` |
| Boarding from the queue (`RideLoadingCoordinator`) | as generated | **wait-adjusted** at take time (§6.5) | as queued |
| Manual board command (`IRideEventSampler.NextRiderProfile`, seeded) | uniform `[0.1, 1]` | uniform `[0.65, 0.85]` | `0` |
| Created without a profile (`Passenger.OfWeight`, tests) | `0.5` | `0.75` | `0` |

The queue and the ride each have their own `RiderProfile` value object with identical
validation (`p ∈ [0.1, 1]`, `H, N ∈ [0, 1]`, NaN and infinity rejected). They are bridged
only by the three plain numbers on `PersonDto`, so neither module depends on the other's
domain. A boarded passenger carries **exactly** the values the queue returned when the
group was taken — nothing is re-rolled at the seat.

---

## 6.5 Queue grumpiness — a pure function of the wait

Happiness erodes in the line, but nothing in the queue *mutates*. The stored person keeps
their arrival happiness; the **current** happiness is computed from how long their group
has waited, whenever it is asked for:

```
waited   = now − queuedAt                        // group timestamp, from TimeProvider
overdue  = max(0, waited − onset)  in minutes     // onset = 5 min (GrumpinessOnset)
H_now    = clamp(H_arrival − rate · overdue, 0, 1) // rate = 0.01 / min (GrumpinessRatePerMinute)
```

Nobody gets grumpy in the first five minutes; after that, `0.01` per minute — a
45-minute wait costs `0.4`. The queue status reports the **average** `H_now` over
everyone waiting (`null` when the line is empty), and `TakeGroupAsync` reports each
member's `H_now` at take time, which is the value they board with (§6.4).

```
H_arrival = 0.8, waited 5 min   ⇒ H_now = 0.8
H_arrival = 0.8, waited 15 min  ⇒ H_now = 0.8 − 0.01·10 = 0.7
H_arrival = 0.3, waited 60 min  ⇒ H_now = clamp(0.3 − 0.55) = 0
```

---

## 6.6 Two clocks

The two halves of the model run on **different clocks**, deliberately:

| Where | Clock | Why |
|-------|-------|-----|
| Queue grumpiness (§6.5) | wall clock, via the injected `TimeProvider` | the line is a real-time process — the filler service already runs on it |
| Ride mood (§6.2, §6.3) | simulated time, the fixed `dt` of the physics tick | determinism (doc 1 §1.1): the same state and inputs must give the same mood |

In production the simulation runs in real time, so the two agree. In tests both are
faked — `FakeTimeProvider` for the queue, an explicit tick count for the ride — and no
test reads a real clock.

---

## 6.7 What this doc gives the test suite

| Behaviour | Assertion |
|-----------|-----------|
| Rest is zero intensity | ride at rest ⇒ every gondola `intensity = 0`, none `atLimit` |
| Intensity scales | `G_h = 2.25 g` ⇒ `intensity = 0.5` |
| Intensity saturates | `G_h = 5 g` ⇒ `intensity = 1`, `atLimit = true` |
| Matched rider gains | `p = 0.6, H = 0.7`, 2 s at `0.6` ⇒ `H = 0.9`; `0.5` and `0.7` both count as matched; capped at `1` |
| Overwhelmed rider suffers | `p = 0.2, H = 0.8, N = 0`, 2 s at `1` ⇒ `H = 0.6, N = 0.4`; `0.75` vs `p = 0.6` is too intense; `N` capped at `1` |
| Tamer ride is a no-op | `p = 0.9` at intensity `0.3` ⇒ `H, N` unchanged |
| Frozen when still | `p = 0.1` rider, 10 s `Loading` or `Safe` ⇒ unchanged; 2 s `Started` ⇒ `H += 0.2` |
| Sustained-G latch | 2 s at limit ⇒ `+0.5 N` once; 5 s ⇒ once; 1 s + dip + 1 s ⇒ none; 2 s + dip + 2 s ⇒ twice; `p = 1` not exempt |
| Profile travels | queued `(0.3, 0.55, 0)` ⇒ seated passenger `(0.3, 0.55, 0)` |
| Roll-up | seated `H = {0.6, 0.8}, N = {0.2, 0.4}` ⇒ `count 2, H̄ = 0.7, N̄ = 0.3`; empty ⇒ `count 0`, averages `null` |
| Grumpiness | `0.8` unchanged at 5 min; `0.7` at 15 min; floor `0`; stored person untouched |
| Observability | telemetry span carries the two averages only; each offloaded rider is one untagged measurement in each final-mood histogram |

---

*Back to the [index](README.md). Default numeric parameters:
[appendix-parameters.md](appendix-parameters.md).*
