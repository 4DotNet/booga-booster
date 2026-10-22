# 6. Rider Experience — Happiness, Intensity & Nausea

The first five documents tell an operator whether the ride is *safe*. This one tells them
whether anyone is *enjoying* it. Every guest carries a **mood** — three scores on a 0–100
scale — that erodes while they wait in line and evolves while they ride. The model is
deliberately small: a closed-form decay in the queue, and three fixed-step rules on the
ride that run inside the existing physics tick.

| Score | 0 means | 100 means |
|-------|---------|-----------|
| Happiness `H` | very sad | extremely happy |
| Preferred ride intensity `P` | gentlest ride | most intense ride |
| Nausea `N` | fine | extremely nauseous |

> **Scope.** Mood is an *output* of the simulation. It never feeds back into physics,
> boarding order or arrival rates, and nausea does not feed into happiness. A guest's mood
> is forgotten when they leave the ride.

---

## 6.1 Arrival

A newly generated guest draws, uniformly and through the queue's seeded randomizer:

```
H₀ ∈ [65, 85]          fairly happy, with some spread
P  ∈ [50, 100]         everyone wants at least a medium ride
N  = 0                 nobody arrives nauseous
```

Both draws happen *after* the name and weight, so the extra randomness only shifts the
seeded sequence from the second guest onward.

A passenger boarded without a known mood (the single-passenger `BoardPassenger`
command, which only knows a weight) gets the **default profile** `H = 75`, `P = 75`,
`N = 0` — the midpoints of the arrival ranges.

---

## 6.2 Queue patience — happiness decays after five minutes

A guest's arrival happiness `H₀` is stored and never mutated. Their happiness after a wait
`w` (measured from the moment their group joined the line) is a pure function:

```
H(w) = H₀                                   w ≤ T_patience
H(w) = H₀ · 2^(−(w − T_patience) / T½)      w > T_patience

T_patience = 5 min      T½ = 10 min
```

The queue evaluates `H(w)` against `TimeProvider.GetUtcNow()` whenever it builds a status
snapshot or hands a group over for boarding. There is no background loop decrementing a
counter: the closed form is exact, needs no shared mutable state, and is trivially testable
with a fake clock.

The curve is continuous at `w = T_patience` (2⁰ = 1), strictly decreasing after it, and
never reaches zero, so `H` stays in `[0, H₀]` without clamping.

### Worked values (`H₀ = 80`)

| Wait `w` | Extra wait `w − 5 min` | Factor `2^(−extra/10 min)` | `H(w)` |
|---------:|------------------------:|---------------------------:|-------:|
| 4 min 59 s | — | 1 | **80** |
| 10 min | 5 min | 2^−0.5 = 0.7071 | 56.57 |
| 15 min | 10 min | 2^−1 = 0.5 | **40** |
| 20 min | 15 min | 2^−1.5 = 0.3536 | 28.28 |
| 30 min | 25 min | 2^−2.5 = 0.1768 | 14.14 |

> **Persistence note.** Because happiness is computed at read time, a future persistent
> queue must store `QueuedAt` and `H₀`, never the decayed value.

The queue status also reports the **average current happiness** over everyone waiting,
and reports it as *absent* (not 0) when nobody is waiting.

---

## 6.3 Ride intensity — felt G over `G_max`

Doc 5 §5.1 defines the felt load as the magnitude of the specific force. The gondola
already tracks its horizontal components `ForwardG` and `LateralG` (in g); gravity adds a
constant 1 g vertically. Per gondola, on every physics step:

```
G_felt    = √(ForwardG² + LateralG² + 1)
intensity = min(100, 100 · G_felt / G_max)          G_max = 4.5 g
```

This reads "50 % of the maximum allowed G-force" literally: **2.25 g is 50 %**.

| Situation | `G_felt` | Intensity |
|-----------|---------:|----------:|
| At rest (§5.1 sanity check) | 1.00 g | 22.2 |
| Peak of the cruise beat (§5.3) | ≈ 2.8 g | ≈ 62 |
| Half of `G_max` | 2.25 g | **50** |
| `G_max` | 4.5 g | 100 |
| Over the limit | 5.0 g | **100** (capped) |

A parked ride sits at 22 %, below every possible preference (`P ≥ 50`), so standing still
pleases nobody. The reachable range under the baseline parameters (cruise peaking near
2.8 g) means a 100 %-rider rarely peaks and the over-G shock (§6.6) is rare in practice;
domain tests drive the gondola's G directly to cover those branches.

*Rejected alternative:* mapping `(G − 1)/(G_max − 1)` so rest is 0 %. It moves the "50 %"
point to 2.75 g and contradicts the stated requirement.

---

## 6.4 Riding happiness — a triangular comfort band

With `Δ = intensity − P` (percentage points), a rider's happiness grows at

```
dH/dt = R_h · max(0, 1 − |Δ| / B)          R_h = 1.0 pt/s,   B = 15 pp
```

then is clamped to `[0, 100]`. The gain is full at an exact match and fades linearly to
zero 15 points either side; outside the band happiness is unchanged. The rate is constant
over a step, so the explicit update `H ← min(100, H + dH/dt · dt)` is exact.

### Worked values

| Rider `P` | Intensity | `|Δ|` | Rate | After 10 s |
|----------:|----------:|------:|-----:|-----------:|
| 50 | 50 | 0 | 1.0 pt/s | **+10** |
| 100 | 100 | 0 | 1.0 pt/s | +10 |
| 100 | 90 | 10 | 1 − 10/15 = 0.333 pt/s | +3.33 |
| 100 | 50 | 50 | 0 | **0** |
| 75 | 75, starting from 99.5 | 0 | 1.0 pt/s | **100** (capped) |

So a 50 %-rider peaks around 2.25 g and a 100 %-rider at 4.5 g, and a well-matched minute
is worth about 60 points — "markedly happier" against a 65–85 start.

---

## 6.5 Riding nausea — exponential past a 30-point over-intensity

When the ride exceeds a rider's preference by `Δ ≥ Δ_N = 30` pp, nausea grows as

```
dN/dt = k · (N + N₀)          k = ln 2 / T_N,   T_N = 10 s,   N₀ = 1
```

A pure `dN/dt = k·N` never leaves 0, and every guest boards at 0; the **seed** `N₀` gives
the growth something to multiply. The ODE is linear in `(N + N₀)`, so it is integrated
**exactly** per step rather than with Euler:

```
N ← (N + N₀) · e^{k·dt} − N₀          then clamp to [0, 100]
```

Exact integration makes the result independent of the step size: many 1/120 s steps
compose to the same value as one long step, `N(t) = (N(0) + N₀)·2^{t/T_N} − N₀`.

### Worked values (`N(0) = 0`, `N₀ = 1`, `T_N = 10 s`)

| Time over-intense | `2^{t/10}` | `N(t) = 2^{t/10} − 1` |
|------------------:|-----------:|----------------------:|
| 10 s | 2 | 1 |
| 20 s | 4 | **3** |
| 40 s | 16 | **15** |
| 60 s | 64 | **63** |
| 66.6 s | 101 | 100 (capped) |

A slow start, then an unmistakable climb. Below the threshold (`Δ < 30`) nausea **holds**:
it does not recover on the ride. A 50 %-rider at 75 % intensity (`Δ = 25`) keeps whatever
nausea they had.

---

## 6.6 Over-G shock — once per sustained episode

Each gondola accumulates the time its riders feel `G_felt ≥ G_max`, and **resets** the
accumulator the moment `G_felt` drops below `G_max`. When the accumulator first exceeds
`T_shock = 1 s`, every occupant gains `N_shock = 25` nausea (clamped to 100), and the
episode is marked as charged. A further shock needs the G to drop below `G_max` and then
hold it again for more than a second.

| Scenario | Shocks | Shock nausea |
|----------|-------:|-------------:|
| 3 s continuously at ≥ 4.5 g | 1 | **25** |
| 0.5 s peak, then below | 0 | **0** |
| 1.5 s above, drop, 1.5 s above | 2 | **50** |

"Every time this occurs" is read as *each time the condition arises*, not *each further
second*: otherwise a ride parked at `G_max` would pin everyone at 100 within four seconds.

The shock is independent of the rider's preference. A rider whose ride is at `G_max` is
also, by §6.3, at intensity 100, so a 100 %-rider can be happy and shocked at once.

---

## 6.7 When mood changes — only in motion

The mood rules (§6.4–§6.6) run inside `Ride.Advance`'s physics branch — `Started`,
`Stopping` and `EmergencyStop` — immediately after each gondola has updated its G-forces.
In `Idle`, `Loading`, `Safe` and `Offloading` nothing changes. Per gondola per step:

```
1. update ForwardG, LateralG                    (doc 5)
2. G_felt, intensity                            (§6.3)
3. over-G accumulator; shock occupants if due   (§6.6)
4. each occupant: comfort-band gain, nausea     (§6.4, §6.5)
```

Every step is a pure function of state at a fixed `dt = 1/120 s`, so the mood model
inherits the twin's determinism: two identical rides replaying the same commands produce
identical moods.

---

## 6.8 Telemetry

Ride telemetry carries a ride-wide summary: the **rider count** and the **average
happiness**, **average preferred intensity** and **average nausea** over every seated
passenger, in any state. With nobody aboard the three averages are *absent*, not 0, so "no
data" is never confused with "0 %". There is no per-seat mood on the wire.

---

## 6.9 What this doc gives the test suite

| Behaviour | Assertion |
|-----------|-----------|
| Arrival ranges | 1,000 guests: `H ∈ [65, 85]`, `P ∈ [50, 100]`, `N = 0` |
| Patience | `H₀ = 80` ⇒ 80 at 4:59, 40 at 15:00, strictly decreasing at 10/20/30 min |
| Intensity | 2.25 g ⇒ 50; 5.0 g ⇒ 100 |
| Comfort band | exact match for 10 s ⇒ +10; `P = 100` at 50 % ⇒ +0; 99.5 ⇒ capped at 100 |
| Nausea | `P = 50` at 85 % ⇒ ≈ 3 / 15 / 63 after 20 / 40 / 60 s; at 75 % it holds |
| Over-G | 3 s hold ⇒ +25; 0.5 s ⇒ +0; two 1.5 s episodes ⇒ +50 |
| In motion only | 60 s of `Loading` leaves mood unchanged |
| Determinism | two identical rides ⇒ identical moods |
| Summary | (60, 80), (50, 100), (0, 20) ⇒ 70 / 75 / 10; empty ⇒ 0 riders, no averages |

---

*Back to the [index](README.md). Default numeric parameters:
[appendix-parameters.md](appendix-parameters.md).*
