# 1. Physics Overview — Frames, the Tick, and Determinism

This document sets up the world the rest of the physics lives in: the coordinate
frames, the state the engine carries, and the single most important rule of the
engine — **it is a deterministic, fixed-timestep function of state**.

---

## 1.1 The deterministic core

The physics engine is a pure function:

```
(state, inputs, dt) ─────► state'
```

Nothing in the hot path reads the wall clock, and nothing is random. Given the same
starting state and the same input sequence, the engine produces bit-for-bit the same
trajectory on every run and every machine. This is not an aesthetic choice — it is what
makes the physics *testable*: you can run 10,000 ticks in a millisecond and assert on
energy conservation, terminal speed, or a settled equilibrium, and a failure is always
reproducible and bisectable.

Consequences that the engine must honour:

- **Fixed timestep.** The integrator always advances by `dt = 1/120 s`. The render/telemetry
  rate is *decoupled* from the physics rate with an accumulator (see §1.4). You never
  integrate with a variable frame time.
- **No wall-clock, no `DateTime.Now`, no ambient RNG** inside a tick.
- **Fault injection is explicit and seeded.** A "random" wind gust or a jammed bearing
  is a scripted, seeded event fed in as an input, never a live `Random.Shared` call.
- **Deterministic initial conditions.** The passive carts are nonlinear oscillators
  (doc 4); their initial yaw `θ_cart` is seeded from a fixed value so runs are reproducible.

---

## 1.2 Coordinate frames & the kinematic hierarchy

Everything spins about one **vertical axis** (the mill spindle). We work in a
right-handed world frame with **+z up**; the interesting dynamics are almost entirely
in the horizontal **x–y plane**, so most of the maths is 2-D rotation plus a constant
`-z` gravity vector.

There are three nested rotating frames. A body at level *n* is positioned by composing
the rotation of its parent with its own arm offset:

```
World
 └─ Mill frame        rotate by θ_mill about the central axis
      └─ Hub i frame   translate by mill-arm a_i (len 6), then rotate by θ_hub[i]
           └─ Cart ij  translate by hub-arm b_j (len 2), then rotate by θ_cart[ij]
                └─ Seat translate by seat offset s (±0.5 m)
```

The world position of any seat is therefore a composition of three planar rotations
plus three offsets:

```
p_world = R(θ_mill) · [ a_i + R(θ_hub[i]) · ( b_j + R(θ_cart[ij]) · s ) ]
```

where `R(θ)` is the 2-D rotation matrix `[[cosθ, −sinθ], [sinθ, cosθ]]`, `a_i` is the
*i*-th mill arm (length 6 m, fixed mount angle `i·90°`), `b_j` the *j*-th hub arm
(length 2 m, fixed mount angle `j·90°`), and `s` the seat offset within the cart.

This one equation drives both the physics (doc 5 differentiates it for accelerations)
and the 3-D rendering (the frontend reads the three angle arrays and rebuilds the geometry).

### Mount angles

| Level | Count | Arm length | Mount angles |
|-------|------:|-----------:|--------------|
| Mill arms | 4 | 6 m | 0°, 90°, 180°, 270° |
| Hub arms (per hub) | 4 | 2 m | 0°, 90°, 180°, 270° |
| Seats (per cart) | 2 | ±0.5 m | either side of the cart pivot |

---

## 1.3 The state vector

The complete simulation state is small and flat — 21 rotating bodies, each with an
angle and an angular rate, plus the seating/occupancy that sets their masses.

```
Ride state
├─ Mill        : { θ, ω }                         driven      (doc 2, 3)
├─ Hub[4]      : { θ, ω }                          driven      (doc 2, 3)
├─ Cart[16]    : { θ, ω }                          passive     (doc 4)
│    └─ Seat[2]: { occupiedKg }                    load input  (doc 2 §inertia)
├─ RideState   : Idle | Loading | … | Faulted      state machine (Plan §6)
└─ inputs      : per-drive throttle 0–1, env (wind), fault flags
```

Angular **acceleration** `α` is *not* stored — it is recomputed each tick from the
current torques and inertia (`α = τ_net / I`). Moment of inertia `I` is *not* stored
either — it is recomputed each tick from who is seated where (doc 2 §2.3). Only the
integrator's true degrees of freedom — the angles and rates — persist between ticks.

**Single writer.** The state is mutated only on the simulation-loop thread. Operator
commands are validated by the state machine and applied at tick boundaries. No locks in
the hot path.

---

## 1.4 The tick

One physics step, executed at fixed `dt`:

```
tick(state, inputs, dt):
    1. Recompute inertia        I_mill, I_hub[i], I_pivot[ij]   from current seating   (doc 2)
    2. Driven bodies            for mill and each hub:
         τ_motor  = motor(throttle, ω)                                                  (doc 3)
         τ_loss   = friction(ω) + drag(ω)                                               (doc 3)
         α        = (τ_motor − τ_loss) / I
         ω += α·dt ;  θ += ω·dt          // semi-implicit Euler                          (doc 2)
    3. Passive carts            for each of the 16 carts:
         compute centrifugal field (A, ψ_outward) at the pivot                          (doc 4, 5)
         τ = m·r_g·A·sin(ψ_outward − ψ_com) − c·ω_cart
         α = τ / I_pivot ;  ω_cart += α·dt ;  θ_cart += ω_cart·dt                        (doc 4)
    4. Derived telemetry        G-forces, jerk, imbalance, energy, temps                 (doc 5)
    5. Safety pass              over-G / over-speed / NaN watchdog → maybe trip EStop    (Plan §6)
```

The ordering matters: inertia is refreshed **before** it is used; the driven bodies are
stepped **before** the carts, because a cart's field depends on the mill/hub speeds.

### Integration order (driven and passive alike)

We use **semi-implicit (symplectic) Euler** everywhere:

```
α  = τ_net / I
ω += α · dt        // update velocity FIRST …
θ += ω · dt        // … THEN position, using the new velocity
```

Updating velocity before position is what makes the scheme *symplectic*: it does not
inject spurious energy, so a coasting body loses energy monotonically (a property test)
and an undamped oscillator does not blow up. Plain (explicit) Euler updates position
from the *old* velocity and slowly pumps energy in — do not use it. Semi-implicit Euler
is stable and cheap; only escalate to RK4 if a test proves you need the accuracy.

### Decoupling render from physics (the accumulator)

The host loop accumulates real elapsed time and drains it in whole `dt` steps, so the
physics rate never depends on how fast frames arrive:

```
accumulator += realElapsed
while accumulator >= dt:
    tick(state, inputs, dt)
    accumulator -= dt
# telemetry is sampled/pushed on its own schedule (e.g. 30 Hz), independent of dt
```

---

## 1.5 Invariants the engine must never violate

These are asserted in development and become the physics test contract:

- **No `NaN`/`Inf` may escape a tick.** AI-written physics loves to produce a stray
  `NaN` (a `0/0` in the motor model, an `atan2(0,0)`). Guard `ω_eps` in the motor
  model, and trip the watchdog to E-stop if any state field goes non-finite.
- **Energy never increases while coasting.** With zero motor power, `Σ ½Iω²` is
  monotonically non-increasing — the direct test of the integrator + losses.
- **Symmetry ⇒ zero imbalance.** A symmetric, centred load produces exactly zero mill
  CoM offset (doc 5 §imbalance). A non-zero result is a sign/indexing bug.
- **Rest = 1.0 g.** With everything stopped, every seat reads exactly 1.0 g (doc 5).
- **Over-speed / over-G are hard caps.** The motor cannot command past `ω_max`; exceeding
  `G_max` auto-reduces power.

See the individual docs for the exact formulas each invariant tests.
