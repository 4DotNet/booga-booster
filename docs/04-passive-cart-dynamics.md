# 4. Passive Cart Dynamics — The Free-Spinning Gondolas

The 16 carts are the soul of the ride and the most interesting physics in the twin.
**They have no motor.** Each rides on an offset vertical pivot and swings *passively*
under the centrifugal field the mill and hubs create. Their motion is emergent — nobody
scripts it — and it depends entirely on **who is sitting where**. This document derives
the model and explains why it produces such rich, per-cart-distinct behaviour.

---

## 4.1 The physical picture

Each cart is mounted on a **vertical pivot** placed toward the *front* of the cart. The
combined centre of mass (CoM) of cart + riders sits *behind* the pivot by a distance `r_g`
(riders lean back against the outer backrest). As the ride spins, the centrifugal field
pushes the cart's CoM outward; because the CoM is offset from the pivot, that outward push
produces a **yaw torque about the pivot**. The cart swings its back outward and tries to
align "CoM points outward."

```
            pivot (vertical axis, offset toward front)
              │
     front ───●───────────┐
              │           │      centrifugal field ──►
              │    CoM  ●  │      pushes CoM outward,
              │     (r_g)  │      torque about pivot swings the back out
     back  ───┴───────────┘
```

### Two simplifications that make this clean

1. **Gravity produces no yaw torque.** The pivot is *vertical*, so gravity (acting down,
   `−z`) has a torque about the pivot that is purely horizontal — it does not rotate the
   cart in yaw. You can **leave gravity out of the cart-yaw equation entirely**. The cart's
   spin is driven purely by the *horizontal* centrifugal component.
2. **It's a driven pendulum.** With gravity gone, the cart is mathematically a pendulum
   whose "gravity" is the horizontal centrifugal field — except that field's *direction*
   rotates as the mill and hubs turn. This is exactly a **driven (parametrically forced)
   pendulum**, which is why the behaviour is so rich (§4.5).

---

## 4.2 The equation of motion

For cart *ij*, yaw angle `θ_cart` about its pivot:

```
I_pivot · θ̈_cart  =  m · r_g · A · sin(ψ_outward − ψ_com)  −  c · θ̇_cart
                     └──────── driving torque ────────┘     └─ damping ─┘
```

| Term | Meaning | Set by |
|------|---------|--------|
| `I_pivot = I_cart_cm + m·r_g²` | cart+riders inertia about the pivot (parallel-axis, doc 2 §2.3) | seating |
| `m` | total mass of cart + riders | seating |
| `r_g` | pivot → CoM distance | seating (more/heavier riders → larger `r_g`) |
| `A` | magnitude of the horizontal centrifugal field at the pivot | mill & hub speeds |
| `ψ_outward` | world direction the field points (outward, away from centres) | geometry & speeds |
| `ψ_com` | world direction the cart's CoM (its back) currently points = `θ_cart + backrestOffset` | state + seating |
| `c` | pivot friction / damping | config (a live "sticky vs free" knob) |

### The restoring torque

The `sin(ψ_outward − ψ_com)` factor is a **restoring torque**:

- **Zero** when the back points straight outward (`ψ_com = ψ_outward`) — the equilibrium.
- **Grows** with misalignment, pulling the back toward "outward."
- It's exactly the `sin` of a pendulum settling toward "down" — but here "down" means
  "outward," and that outward direction *sweeps around* as the ride rotates.

The damping term `−c·θ̇_cart` bleeds energy so the swing eventually settles instead of
ringing forever.

---

## 4.3 The per-tick recipe (each of the 16 carts)

No motor term — the driving comes entirely from the field:

1. **Field at the pivot.** The pivot's world position `P` is already known from the
   nested-frame kinematics (doc 1 §1.2). Approximate the horizontal acceleration of the
   pivot as the sum of the two parent centripetal contributions:

   ```
   a_P ≈ ω_mill² · (P − mainAxis)  +  ω_hub² · (P − hubCentre)      // horizontal parts
   ```

   (This is the cheap-but-good approximation; it ignores the Euler/Coriolis transients,
   which are small for the cart-swing driving and can be added later if a test demands.)

2. **Magnitude & direction:**
   ```
   A         = |a_P|
   ψ_outward = atan2(a_P.y, a_P.x)          // points outward, away from the centres
   ```

3. **Where the back currently points:**
   ```
   ψ_com = θ_cart + backrestOffset          // backrestOffset shifts with sideways seating
   ```

4. **Net torque:**
   ```
   τ = m·r_g·A·sin(ψ_outward − ψ_com) − c·θ̇_cart
   ```

5. **Integrate (semi-implicit Euler, doc 1 §1.4 — same scheme as the driven bodies):**
   ```
   θ̈       = τ / I_pivot
   θ̇_cart += θ̈ · dt
   θ_cart += θ̇_cart · dt
   ```

The emergent `ω_cart = θ̇_cart` from step 5 is a *real telemetry channel*
(`cart[i][j].rpm`) and feeds the third term of the G-force superposition (doc 5) — the
cart's own swing enriches the felt-G beat pattern.

---

## 4.4 Why the seating chart *is* the behaviour

This is what makes the design delightful rather than merely faithful — load coupling
reaches the carts **directly** through `m`, `r_g`, and `backrestOffset`:

- **Two heavy riders** → larger `m` *and* larger `r_g` → the driving torque `m·r_g·A`
  is much bigger → the cart swings **harder** and is dragged over the top more easily.
- **One rider, left seat only** → the CoM shifts sideways → `backrestOffset` changes →
  the cart settles at a **lopsided** equilibrium and swings **asymmetrically**.
- **A light, balanced cart** on the same ride → small driving torque → it barely turns
  while its heavy neighbour tumbles.

So all 16 carts can behave completely differently on the *same* ride, purely from where
the weight sits. "Seating-chart roulette" (Plan §12) is a direct, free consequence of this
model — not a special feature you have to build.

---

## 4.5 Slow oscillation vs. going "over the top"

A driven pendulum has two qualitatively different regimes, and the cart shows both:

- **Slow field (mill/hubs turning gently).** `ψ_outward` sweeps slowly; the cart can keep
  its back pointed outward and just **oscillates** gently about the outward direction.
- **Fast field (spun up).** `ψ_outward` sweeps faster than the cart can track. The field
  drags the cart "over the top" into **continuous, uneven rotation** — the same
  slow-then-tumbling transition every driven pendulum exhibits. It looks wild and
  unpredictable, but it is fully deterministic (§4.7).

Nothing about this is scripted. It emerges from the seating (`m`, `r_g`, damping `c`) and
the mill/hub speeds, and it flows straight into the G-force pulsing.

---

## 4.6 The unbluffable test

> **Spin the ride at constant speed with a symmetric, centred load. Every cart's back
> must settle pointing radially outward** (`ψ_com → ψ_outward`, `θ̇ → 0`).

At constant mill/hub speed and a centred load (`backrestOffset = 0`), the only stable
equilibrium of the driven-pendulum equation is the back pointing straight out. Any *other*
settled angle means a sign error, a wrong offset, or a bad frame — the physics cannot lie
about this. It is the single sharpest test for the whole cart model.

Secondary checks:

- Heavier / eccentric seating ⇒ larger swing amplitude / lopsided equilibrium (§4.4).
- With `c = 0` (frictionless pivot) an undamped small oscillation must **not gain energy**
  — a direct test of the symplectic integrator on the cart.

---

## 4.7 Two implementation notes

1. **Keep `c` small but non-zero.** With zero damping the carts ring forever; with too
   much they glide statically. Expose `c` as a live **"free pivot vs sticky pivot"** knob
   (default `900 N·m·s/rad`) — low damping gives wild, long-ringing swings; high damping
   gives a stately glide.
2. **Seed `θ_cart` deterministically.** This is a **nonlinear driven oscillator**, so it
   is sensitive to initial conditions. Seed each cart's initial yaw from a fixed,
   reproducible value (not an RNG) so test runs are bit-for-bit repeatable and the golden
   scenario is stable.

---

## 4.8 What this doc gives the test suite

| Behaviour | Assertion |
|-----------|-----------|
| Correct equilibrium | constant speed + symmetric centred load ⇒ every back settles radially outward |
| Load coupling | heavier riders ⇒ larger swing amplitude at the same ride speed |
| Asymmetry from seating | one-sided load ⇒ lopsided (`backrestOffset`-shifted) equilibrium |
| No energy from nowhere | `c = 0`, constant field ⇒ oscillation amplitude non-increasing |
| Determinism | fixed seed ⇒ identical `θ_cart(t)` across runs |

Next: how all these rotations combine into what a rider actually *feels* — [forces & G-forces](05-forces-and-g-forces.md).
