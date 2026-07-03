# 5. Forces & G-Forces — What the Rider Actually Feels

This document turns the three nested rotations into the number that sells the whole
simulation: the **G-force** at each seat, and the emergent **beat pattern** that makes it
pulse. It also covers the rotating imbalance ("wobble") that lopsided loading produces.

---

## 5.1 Specific force — what a G-meter actually reads

A G-meter (and a rider's inner ear) does **not** read acceleration. It reads **specific
force** — the non-gravitational force per unit mass, i.e. the force the seat exerts on you:

```
f = a − g
```

where `a` is the seat's coordinate (world) acceleration and `g` is the gravity vector
(`g = (0, 0, −9.81) m/s²`). The felt load in g's is:

```
G = |f| / 9.81
```

### Sanity check: at rest, G = 1.0

At rest the seat isn't accelerating, so `a = 0`:

```
f = 0 − g = −g = (0, 0, +9.81)        // the seat pushing UP on you
G = |f| / 9.81 = 1.0
```

Exactly **1.0 g, upward** — the everyday sensation of sitting still. This is a hard test:
with everything stopped, every one of the 32 seats must read `G = 1.000`.

---

## 5.2 Seat acceleration from the nested frames

The seat's world position is the composition of three rotations (doc 1 §1.2):

```
p_world = R(θ_mill) · [ a_i + R(θ_hub[i]) · ( b_j + R(θ_cart[ij]) · s ) ]
```

The coordinate acceleration `a = p̈_world` is the second time-derivative of this. Rather
than differentiate the full expression, we use the fact that **centripetal accelerations
from nested rotating frames superpose as vectors**.

### Steady-state (constant ω's) — the practical model

When the three angular rates are constant, the tangential (Euler) terms vanish and only
the centripetal contributions plus gravity survive. Each frame contributes an inward
acceleration `−ω² · r_vec` toward *its own* axis:

```
a_seat ≈ (−ω_mill² · r_mill_vec)      // toward the main central axis
       + (−ω_hub²  · r_hub_vec)       // toward this hub's centre
       + (−ω_cart² · r_cart_vec)      // toward this cart's centre
f = a_seat − g
G = |f| / 9.81
```

- `r_mill_vec` — vector from the central axis to the seat (magnitude ≈ orbit radius at mill level).
- `r_hub_vec` — vector from the hub centre to the seat.
- `r_cart_vec` — vector from the cart centre to the seat (the small ±0.5 m seat offset).
- `ω_cart` here is the **emergent** yaw rate from the passive swing (doc 4) — *not* a
  commanded input. So the third term ebbs and flows with the cart's own oscillation.

Each term points toward a *different* centre, and — crucially — those directions **rotate
at different rates** (`ω_mill`, `ω_hub`, `ω_cart` all differ). That mismatch is the source
of the beat pattern below.

### Full accuracy (spin-up / spin-down)

During ramps the ω's are changing, so add the two transport-theorem terms per frame:

```
Euler (tangential):  α × r          // from angular acceleration during ramps
Coriolis:            2·ω_outer × v_inner   // cross-frame coupling of moving inner bodies
```

For a first cut the steady-state centripetal sum is plenty and matches the worked example
below. Graduate to the full expansion only when a test on a *ramp* demands the extra
accuracy.

---

## 5.3 The beat — why felt-G pulses (the "wow")

Worked example from the plan (§4.5), the moment that sells the twin:

```
mill  ω = 1.5 rad/s   at r_mill ⇒ inward accel = ω²r = 13.5 m/s²
hub   ω = 2.0 rad/s   at r_hub  ⇒ inward accel =        8.0 m/s²
cart  ω = 3.0 rad/s   at r_cart ⇒ inward accel =        4.5 m/s²
```

These three horizontal vectors point in different directions that each rotate at a
different rate. Their **vector sum** therefore sweeps through phases:

- **Opposed** (vectors partly cancel): horizontal magnitude dips to **~1 m/s²**.
- **Aligned** (vectors reinforce): horizontal magnitude peaks at **~26 m/s²**
  (13.5 + 8.0 + 4.5).

Combine the horizontal sum with the constant `9.81 m/s²` of gravity (vertically), and the
felt load **pulses** between roughly **1.0 g** (everything opposed) and a peak of about
**2.8 g** (everything aligned), oscillating as the three frames phase in and out of
alignment.

That beat is **emergent**, not scripted — it's the interference of three incommensurate
rotation rates, essentially a Lissajous figure you can ride. Surface it on
`cart[i][j].gForce` (instantaneous + rolling peak) and it's the most compelling channel in
the whole system. Rotation-rate *ratios* that are simple fractions give clean, repeating
beats; irrational-ish ratios give chaotic, never-repeating ones — a knob worth exposing.

---

## 5.4 Jerk — the "scream index"

What actually makes people shriek is not steady G but its **rate of change** — jerk:

```
jerk = d(G)/dt          // units: g/s
```

Compute it as a finite difference of the G channel between telemetry samples. High jerk
(rapid onset of load, e.g. as a cart snaps "over the top" — doc 4 §4.5) is the discomfort
/ thrill metric. Drive a visualization or audio from it; score rides on peak *sustained*
G within safety limits (the thrill-vs-safety leaderboard, Plan §12).

---

## 5.5 Rotating imbalance — the wobble

If the load is uneven across the four mill arms, the mill's combined centre of mass shifts
off the spin axis by an eccentricity `e`. A rotating off-axis mass produces a rotating
imbalance force at the mill frequency:

```
F_imbalance = m_total · e · ω_mill²          // rotates with the mill → a wobble
```

- Compute `e` from the passenger distribution across the four mill arms (the seat weight
  sensors give the masses; the arm geometry gives the moment arms).
- Feed `e` to `mill.imbalanceMm` and the resulting force band to `mill.vibration`
  (`= e · ω_mill²`), a vibration telemetry channel.
- **Gate dispatch on it:** refuse to dispatch (or cap speed) if `imbalanceMm` exceeds the
  limit (default 50 mm) — this is the load-balance safety interlock (Plan §6).

**Symmetry test:** a symmetric, centred load must give `e = 0` exactly, hence zero
imbalance and zero vibration. A non-zero result on a symmetric load is a sign or indexing
bug — one of the most valuable property tests in the suite.

Note `F = m·e·ω²` scales with `ω²`, so a small imbalance that is harmless at idle becomes
a violent wobble at cruise — which is exactly why the interlock gates *before* dispatch.

---

## 5.6 Structural stress (free extra telemetry)

The centripetal force on each arm creates a bending moment at its root:

```
arm.bendingMomentNm = F_centripetal · L = (m_tip · ω² · r) · L
```

A cheap structural-stress channel (`arm[*].bendingMomentNm`) that rises with load and speed
— useful for the dashboards and for future structural-limit interlocks.

---

## 5.7 What this doc gives the test suite

| Behaviour | Assertion |
|-----------|-----------|
| Rest sensation | everything stopped ⇒ every seat reads exactly `1.000 g` |
| Steady-state G is correct | worked example (§5.3) matches the hand computation (peak ≈ 2.8 g) |
| The beat is real | felt-G oscillates between the opposed-min and aligned-max over a cycle |
| Symmetry ⇒ no wobble | symmetric centred load ⇒ `imbalanceMm = 0`, `vibration = 0` |
| Over-G interlock | any seat exceeding `G_max` (4.5 g) auto-reduces power |
| No `NaN` in the felt-force path | `atan2`/normalisation guarded; watchdog trips on non-finite G |

---

*Back to the [index](README.md). Default numeric parameters:
[appendix-parameters.md](appendix-parameters.md).*
