# 2. Rotational Dynamics & Moment of Inertia

This is the beating heart of the twin: how every rotating body turns torque into motion,
and how the load on board changes how hard it is to spin. It applies directly to the **5
driven bodies** (mill + 4 hubs). The passive carts obey the same integrator but with a
different torque source — see [doc 4](04-passive-cart-dynamics.md).

---

## 2.1 Newton's second law for rotation

The rotational analogue of `F = ma` is:

```
τ_net = I · α          ⇒     α = τ_net / I
```

with

```
τ_net = τ_motor − τ_friction − τ_drag
```

`τ_motor` is the drive torque (doc 3 §3.1). `τ_friction` and `τ_drag` are the losses
(doc 3 §3.3). Angular velocity and angle follow by integration:

```
ω = ∫ α dt
θ = ∫ ω dt
```

Two coupled first-order ODEs per body. We solve them numerically.

---

## 2.2 The integrator (semi-implicit Euler)

Advance every body by the fixed timestep `dt = 1/120 s`, **velocity before position**:

```
α  = τ_net / I           // torques and I evaluated at the CURRENT state
ω += α · dt              // 1) velocity update
θ += ω · dt              // 2) position update, using the just-updated ω
```

### Why this scheme

- **Symplectic ⇒ energy-honest.** Because θ is advanced with the *new* ω, the method
  conserves energy over long runs instead of pumping it in (explicit Euler) or bleeding
  it out (implicit Euler). Our coast-down energy test depends on this.
- **Stable and cheap.** One force evaluation per body per tick. No matrix solves.
- **Deterministic.** Fixed `dt`, no adaptivity.

Escalate to **RK4** only if a specific test shows semi-implicit Euler is too inaccurate
(unlikely at 120 Hz for these speeds). Never make `dt` variable — that breaks determinism.

### Worked micro-example (one mill tick)

Suppose at some tick the mill has `I = 2.0×10⁵ kg·m²`, `ω = 1.0 rad/s`, and the net
torque works out to `τ_net = 40,000 N·m`. Then:

```
α  = 40000 / 2.0e5      = 0.20 rad/s²
ω  = 1.0 + 0.20·(1/120) = 1.001667 rad/s
θ += 1.001667·(1/120)   = advances ~0.00835 rad this tick
```

Small per tick — but at 120 ticks/s the mill reaches cruise in ~15–25 s on 90 kW, which
is the believable ramp we want.

---

## 2.3 Load-dependent moment of inertia

`I` is **recomputed every tick** from who is on board. This is what makes load matter:
the *same* power produces a *different* speed depending on how full the ride is, because
a heavier ride has a larger `I`, hence a smaller `α`, and takes longer to reach the speed
where motor torque balances the losses.

We use a **lumped-parameter** model: arms as uniform rods, hub assemblies / carts /
passengers as point masses at their orbit radius.

### Building blocks

- **Uniform rod about one end:** `I_rod = (1/3) · m · L²`
  (a mill/hub arm pivots about the central axis, so we use the end-pivot form).
- **Point mass at radius r:** `I_point = m · r²`
- **Parallel-axis theorem** (for an offset pivot): `I = I_cm + m · d²`, where `d` is the
  distance from the centre of mass to the rotation axis. Used for the carts (doc 4).

### Great Mill (about the central vertical axis)

```
I_mill = Σ over 4 arms  (1/3 · m_arm · L_mill²)              // the four 6 m arms
       + Σ over 4 hubs   M_hub_assembly · r_hub²              // hubs orbiting at r_hub = 6 m
```

where `M_hub_assembly` = hub structure + its 4 cart-arms + its 4 carts + all passengers
in those carts. The hub assemblies sit at `r_hub = L_mill = 6 m`, so they dominate `I_mill`.

### Hub *i* (about its own vertical axis)

```
I_hub[i] = Σ over 4 arms  (1/3 · m_hubArm · L_hub²)           // the four 2 m arms
         + Σ over 4 carts  (m_cart + m_passengers) · r_cart²   // carts orbiting at r_cart = 2 m
```

### Cart *ij* (about its own **offset pivot**)

Because the cart pivot is offset from the cart's centre of mass, we need the parallel-axis
theorem:

```
I_pivot[i][j] = I_cart_cm + (m_cart + m_passengers) · r_g²    // r_g = pivot→CoM distance
```

This is the inertia the passive-swing equation uses (doc 4). Both `m` and `r_g` depend on
seating, so `I_pivot` is per-cart and changes as passengers board.

### Why the passenger terms dominate

Two 75 kg passengers in a cart at `r_hub = 6 m` contribute `150 · 6² = 5,400 kg·m²` to
`I_mill` *through their orbit radius alone*, before counting the cart and arm masses. A
**full hub** (4 carts + 8 people ≈ 1,200 kg orbiting at 6 m) adds on the order of
**~43,000 kg·m²** to `I_mill`. Fill all four hubs and the mill's inertia roughly doubles
from its empty value — the twin becomes visibly more sluggish to spin up as it loads. That
sluggishness is the whole point of the load model, and it falls straight out of `I = Σ m r²`.

---

## 2.4 Kinetic energy (a free, high-value telemetry channel)

Total rotational kinetic energy in the system is:

```
E_kin = Σ over all 21 bodies  ½ · I · ω²
```

Two reasons to compute it every tick:

1. **It's the integrator's lie-detector.** During coast-down (motor off) `E_kin` must be
   monotonically non-increasing — friction and drag only remove energy. If a test ever
   sees `E_kin` rise while coasting, the integrator or a sign is wrong. This is the single
   most valuable property test in the suite.
2. **It's satisfying to watch.** Plotting `E_kin` during a ramp shows the energy pouring
   into the system and levelling off at terminal speed.

---

## 2.5 What this doc gives the test suite

| Behaviour | Assertion |
|-----------|-----------|
| Torque produces the right acceleration | `α == τ_net / I` for known inputs |
| Symplectic integrator is energy-honest | coasting `E_kin(t+1) ≤ E_kin(t)` for all t |
| Load changes dynamics | doubling passenger mass at fixed power lengthens time-to-cruise |
| Inertia formula is correct | `I_mill` for a known seating matches a hand computation |
| No energy from nowhere | with `τ_motor = 0`, ω decays monotonically to 0 |

Next: where `τ_motor` comes from — the [motor, power and torque](03-motor-power-and-torque.md) model.
