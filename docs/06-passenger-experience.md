# 6. Passenger Experience — Happiness, Intensity & Nausea

The first five documents describe what the machine does. This one describes what the
**people** make of it. Every guest carries three ratings — **happiness**, **preferred ride
intensity** and **nausea** — and this document derives how waiting in the queue and the
gondola's felt G-forces change them.

The same rules as the rest of the physics apply: every update is a **pure, deterministic
function of state and elapsed time**, every constant is named (in `RideParameters` on the
ride side, on the `QueuePatience` domain service on the queue side), and every update uses a
**closed-form step** so the result does not depend on the size of `dt`.

---

## 6.1 The three ratings

| Rating | Symbol | 0 means | 100 means |
|--------|--------|---------|-----------|
| Happiness | `H` | very sad | extremely happy |
| Preferred ride intensity | `P` | wants the tamest ride | wants the most intense ride |
| Nausea | `N` | not nauseous at all | maximally nauseous |

All three are real numbers on the closed range **`[0, 100]`** (`MinExperienceRating = 0`,
`MaxExperienceRating = 100`). Two rules keep them there:

- **Construction validates.** A guest created with a rating outside the range is a
  programming or data error and is rejected with a `DomainValidationException`.
- **Evolution clamps.** Every update below computes a new value and clamps it to
  `[0, 100]`. Saturation is a legitimate outcome — nausea 90 plus a 25-point penalty is
  nausea 100, not an error.

### Initial ratings on arrival

A guest generated on arrival at the queue starts with

```
H₀ ~ U[65, 85]      fairly happy to quite happy
P  ~ U[50, 100]     everyone came for a ride, nobody for a tame one
N₀ = 0              nobody arrives nauseous
```

The draws go through the queue's seeded person generator, so a given seed always produces
the same guests (doc 1 §1.4, determinism).

### Passengers boarded without a queue record

An operator can also board a passenger directly, bypassing the queue. Such a passenger has no
drawn ratings, so they get the **means of the arrival distributions**: happiness 75
(`DefaultRiderHappiness`), preferred intensity 75 (`DefaultRiderPreferredIntensity`) and
nausea 0. A typical guest, not an extreme one.

---

## 6.2 Queue erosion — patience runs out

Waiting is free for a **grace period** `w_g = 5 min` (`QueuePatience.GracePeriod`). Past that,
happiness decays exponentially with the *extra* waiting time:

```
H(w) = H₀                             for w ≤ w_g
H(w) = H₀ · e^(−(w − w_g) / τ_q)      for w > w_g
```

with the **queue-patience time constant** `τ_q = 10 min` (`QueuePatience.DecayTimeConstant`).

| Waited `w` | Factor `H/H₀` | Guest arriving at 80 |
|-----------:|--------------:|---------------------:|
| 4 min 59 s | 1.000 | 80.0 |
| 5 min | 1.000 | 80.0 |
| 8 min | 0.741 | 59.3 |
| 11 min | 0.549 | 43.9 |
| 15 min | 0.368 | 29.4 |
| 25 min | 0.135 | 10.8 |

**Why exponential?** It is what the feature request asks for, and it has the property the
tests pin: equal extra waits multiply happiness by equal factors, so
`H(11 min) / H(8 min) = H(8 min) / H(5 min) = e^(−0.3)`. It also never goes negative, so the
clamp at 0 is only a guard.

**Why `τ_q = 10 min`?** A guest who waits a quarter of an hour keeps about a third of their
happiness — visibly unhappy, not yet miserable. That puts the interesting operator decisions
(how long a queue is acceptable?) in the 10–20 minute range a real ride sees.

### Computed at read time, not ticked

`H(w)` is a pure function of arrival happiness and elapsed time, so the queue does not need a
ticker. It stores each group's **join time** (from `TimeProvider`) and the guest's arrival
happiness, and evaluates `H(w)` whenever it reports a guest — in the queue status and at the
moment the group is taken to board. The result is exact whenever it is read, and simulated
time (a `FakeTimeProvider` in tests) drives it directly.

---

## 6.3 Experienced intensity — felt G over the maximum

While the ride's physics runs (Started, Stopping, EmergencyStop), each gondola has an
**experienced intensity** on the same 0–100 scale as the preference:

```
G_h = √(G_forward² + G_lateral²)               horizontal felt G (doc 5 §5.1–5.2)
I   = min(100, 100 · G_h / G_max)              with G_max = 4.5 g (MaxGForce)
```

Both riders of a gondola experience that gondola's intensity.

| `G_h` | `I` |
|------:|----:|
| 0 g | 0 |
| 2.25 g | 50 |
| 4.5 g | 100 |
| 5.0 g | 100 (capped) |

**Why the vector magnitude?** The body feels the resultant force, not its components
separately; taking the larger axis would under-report a gondola swinging diagonally.

**Why horizontal only?** `G_forward` and `G_lateral` are the readings the gondolas already
publish. Folding in the vertical 1 g of gravity would change what those readings mean and
put every stationary rider at intensity 22. A gravity-inclusive magnitude belongs with a
future over-G interlock, not here.

**Why `G_max` as the reference?** The feature request frames intensity as a fraction of the
maximum *allowed* G-force. `G_max` is used only as that reference and as the episode
threshold in §6.6; this chapter adds no intervention when it is exceeded.

---

## 6.4 Happiness — a Gaussian around the preference

Let `Δ = I − P` be the gap between what the rider gets and what they want. While the
physics runs, happiness rises at

```
dH/dt = k_h · e^(−(Δ / σ_h)²)
```

with the **peak gain rate** `k_h = 1.5 pt/s` (`HappinessGainRate`) and the **match width**
`σ_h = 12 pt` (`HappinessMatchWidth`).

| `|Δ|` | Rate as a fraction of peak | Rate |
|------:|---------------------------:|-----:|
| 0 | 100 % | 1.50 pt/s |
| 6 | 77.9 % | 1.17 pt/s |
| 12 | 36.8 % | 0.55 pt/s |
| 20 | 6.2 % | 0.09 pt/s |
| 30 | 0.19 % | 0.003 pt/s |

So a rider preferring 50 is happiest when the gondola feels about 2.25 g; a rider preferring
100 is happiest right at 4.5 g; and a mismatch of 30 points or more is, for practical
purposes, worth nothing.

**Why a Gaussian?** "Significantly happier around the preference" calls for a sharp peak
with smooth tails. The Gaussian is symmetric in `Δ` (too tame and too wild are penalised
alike), has no kink to test around, and `σ_h = 12` makes the tail negligible exactly where
the nausea rule (§6.5) takes over.

**Why never negative?** The ride itself does not make anyone sadder; an intensity mismatch
costs the rider the gain, and an excess makes them *nauseous* instead. Nausea feeding back
into happiness is explicitly out of scope.

### The step

The rate depends on `I` and `P` only, not on `H`, and both are constant over one step, so
the exact step is simply

```
H' = clamp(H + k_h · e^(−(Δ/σ_h)²) · dt, 0, 100)
```

---

## 6.5 Nausea — exponential growth past a 30-point excess

When the intensity exceeds the preference by at least the **excess threshold**
`Δ_n = 30 pt` (`NauseaExcessThreshold`), nausea grows:

```
dN/dt = r_n + λ_n · N        if I − P ≥ Δ_n
dN/dt = 0                    otherwise
```

with the **base rate** `r_n = 1 pt/s` (`NauseaBaseRate`) and the **growth rate**
`λ_n = 0.15 /s` (`NauseaGrowthRate`). Below the threshold — including any intensity *below*
the preference — the ride does not change nausea, and nausea never decreases while riding.

**Why `r_n + λ_n·N` and not `λ·N`?** Pure `λ·N` is exponential, but its solution from
`N = 0` is `N = 0` forever — and every guest starts at nausea 0. The constant term gets
growth started; the proportional term makes it self-reinforcing, which is the "exponential"
the request asks for.

### The exact step

The ODE is linear, so it has a closed-form solution. Shifting by the fixed point
`N* = −r_n/λ_n` turns it into pure exponential growth:

```
d(N + r_n/λ_n)/dt = λ_n · (N + r_n/λ_n)

⇒  N' = (N + r_n/λ_n) · e^(λ_n · dt) − r_n/λ_n,     then clamp to [0, 100]
```

This is exact for any `dt` — the same philosophy as the symplectic integrator in doc 2: the
update does not drift if the timestep changes, and tests can assert closed-form values.

| Starting at `N = 0`, sustained excess | `N` |
|--------------------------------------:|----:|
| 1 s | 1.08 |
| 5 s | 7.45 |
| 10 s | 23.2 |
| 15 s | 56.6 |
| 18.5 s | 100 (saturates) |

Equal intervals add ever more nausea — the property the tests pin.

**Why these values?** A rider whose ride is far too wild should be visibly queasy within
about ten seconds and maxed out within twenty: long enough for an operator watching the
dashboard to react, short enough to matter in a demo-length ride cycle.

---

## 6.6 Sustained maximum G — a fixed penalty per episode

Independently of the preference, each gondola tracks **max-G episodes**. An episode is a
continuous stretch during which `G_h ≥ G_max`. The gondola keeps two pieces of state:

```
elapsed   — time spent continuously at or above G_max
penalised — whether this episode has already been penalised
```

Each step:

```
if G_h ≥ G_max:
    elapsed += dt
    if elapsed > T_ep and not penalised:
        N += P_ep for each occupant (clamped at 100)
        penalised = true
else:
    elapsed = 0, penalised = false
```

with the **episode duration** `T_ep = 1 s` (`MaxGEpisodeDuration`) and the **penalty**
`P_ep = 25 pt` (`MaxGNauseaPenalty`).

| Scenario | Penalty per rider |
|----------|------------------:|
| 0.8 s at ≥ 4.5 g, then below | 0 |
| 3 s continuously at ≥ 4.5 g | 25 (once) |
| Two separate > 1 s episodes | 50 |
| Empty seat during an episode | — (only occupants are penalised) |

**Why once per episode?** The request says riders gain 25 "every time this occurs". An
episode is the occurrence; dropping below `G_max` ends it, and the next one counts from zero.

**Is it reachable?** The hub alone gives `r·ω² / g = 2 m × (5 rad/s)² / 9.81 ≈ 5.1 g` at the
hub speed cap (`HubMaxAngularVelocity`), so a hub spun up to its cap crosses `G_max`. Domain tests drive the gondola with synthetic G
values either way.

---

## 6.7 When the ratings change

| Phase | Queue erosion | Intensity → happiness / nausea | Max-G episode |
|-------|:---:|:---:|:---:|
| Waiting in the queue | ✅ (read time) | — | — |
| Boarded, ride Loading | — | — | — |
| Started / Stopping / EmergencyStop | — | ✅ every physics step | ✅ every physics step |
| Offloading | — | — | — |

Boarding carries the guest's **waited** happiness (`H(w)` at the moment the group is taken),
preferred intensity and nausea onto the ride. Rider evolution runs only inside the gondola's
physics step, so it is automatically confined to the states in which the physics runs.
Offloading ends the record: per-guest history after the ride is out of scope.

---

## 6.8 What this doc gives the test suite

- Queue: 4 min 59 s leaves happiness unchanged; `H(11)/H(8) = H(8)/H(5)`; erosion follows a
  `FakeTimeProvider`.
- Generator: 1,000 guests all inside the initial ranges; same seed, same guests.
- Intensity: 2.25 g → 50; 5 g → 100.
- Happiness: `P = 50` gains more at `I = 50` than at `I = 80`; `P = 100` gains more at
  `I = 100` than at `I = 50`; no step ever lowers happiness.
- Nausea: excess 29 → no change, excess 30 → growth; the exact-step closed form; clamp at 100.
- Max-G: 0.8 s → nothing; 3 s → one penalty; two episodes → two penalties; empty seats
  untouched.

Default values and a sanity check are in **[appendix-parameters.md](appendix-parameters.md)**.
