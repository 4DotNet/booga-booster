# 3. Motor, Power & Torque — Turning Throttle into Spin

The operator does not command speed or torque directly — they command **power**
(a throttle, 0–100 %). This document explains how power becomes torque, why the same
power gives different speeds under different loads, and the friction/drag losses that
give the ride a natural terminal speed and a realistic coast-down. It applies to the
**5 driven axes** (mill + 4 hubs); the carts have no motor.

---

## 3.1 The motor model — power → torque

A real electric drive **cannot** deliver infinite torque at zero speed, and it is
**power-limited** at high speed. We model exactly that with a capped power / stall-torque
curve.

Commanded mechanical power from the throttle:

```
P_cmd = throttle · P_max               // throttle ∈ [0, 1],  P_max = 90 kW (mill)
```

Torque delivered at the current angular speed ω:

```
τ_motor = min( τ_stall , P_cmd / max(|ω|, ω_eps) )      // magnitude
```

signed by the commanded drive direction. Two regimes fall out of this single expression:

```
τ_motor
  ▲
  │────────────●                     ← constant-torque region: τ = τ_stall
  │            ╲                        (low speed; the motor is torque-limited)
  │             ╲___                  ← power-limited region: τ = P_cmd / ω
  │                 ╲____               (high speed; τ falls as 1/ω)
  │                      ╲______
  └───────────────────────────────►  ω
              ω*      (ω where P_cmd/ω drops below τ_stall)
```

- **`τ_stall`** — the maximum torque the drive can produce, the flat top of the curve.
  At low speed the `min(...)` picks this. (Mill default: `60,000 N·m`.)
- **`P_cmd / ω`** — the power-limited branch. Once the ride is spinning fast enough that
  `P_cmd / ω < τ_stall`, torque falls off as `1/ω`. This is why a fixed power setting
  produces a well-defined top speed rather than accelerating forever.
- **`ω_eps`** — a small floor (e.g. `1e-3 rad/s`) inside the `max(...)` so that at a dead
  stop we compute `P_cmd / ω_eps`, clamp it to `τ_stall`, and never divide by zero.
  **This guard is the classic `NaN` source** the watchdog exists to catch — keep it.

### Why the same power yields different speeds under different loads

This is the headline behaviour the plan asks for, and it is *emergent* from the model,
not scripted:

- Load does **not** appear in the motor curve at all — `τ_motor` depends only on throttle
  and ω.
- Load appears in the **inertia** `I` (doc 2 §2.3) and therefore in the *acceleration*
  `α = (τ_motor − τ_loss) / I`.
- A heavier ride has larger `I` → smaller `α` → it takes **longer** to climb the ω axis to
  the point where `τ_motor` has fallen (along the `P/ω` branch) to exactly balance the
  losses. That balance point is the terminal speed (§3.4).

So heavier loads reach cruise *more slowly*, and — because drag grows with ω (§3.3) — a
much heavier load can also settle at a slightly *lower* terminal ω for the same power.
Both effects come for free from `τ = Iα` plus a power-limited motor.

---

## 3.2 Power delivered vs power commanded

Commanded power is `P_cmd = throttle · P_max`. The power actually **delivered to the
shaft** is `P_shaft = τ_motor · ω`. In the constant-torque (low-speed) region these
differ — at a standstill `P_shaft = 0` no matter the throttle, because `τ_stall · 0 = 0`.
As ω climbs into the power-limited region, `P_shaft → P_cmd`. Expose both on telemetry
(`mill.power` = commanded; `drive[*].torque` = delivered τ) — the gap between them during
spin-up is physically correct and worth visualising.

---

## 3.3 Losses — friction & aerodynamic drag

Two loss torques oppose motion. Together they give a natural terminal speed and a
realistic coast-down when power is cut.

### Bearing friction (Coulomb + viscous)

```
τ_friction = C_coulomb · sign(ω)  +  C_viscous · ω
```

- **Coulomb** (`C_coulomb · sign(ω)`) — a constant magnitude opposing the direction of
  motion; the "stiction-like" dry-bearing term. Note the `sign(ω)`: near ω = 0 this term
  flips sign, so implement it carefully to avoid chattering (clamp to zero within a tiny
  dead-band around ω = 0 so a stopped ride stays stopped rather than jittering).
- **Viscous** (`C_viscous · ω`) — grows linearly with speed; lubricated-bearing drag.

### Aerodynamic drag

```
τ_drag = k_aero · ω · |ω|            // ∝ ω², opposing motion
```

Air resistance on the arms and carts scales with the square of speed. Writing it as
`ω · |ω|` (rather than `ω²`) preserves the correct sign for either spin direction.

---

## 3.4 Terminal speed (where it all balances)

At steady state the body stops accelerating, so `α = 0` and net torque is zero:

```
τ_motor(ω_terminal)  =  τ_friction(ω_terminal) + τ_drag(ω_terminal)
```

In the power-limited region this is:

```
P_cmd / ω_terminal  =  C_coulomb + C_viscous·ω_terminal + k_aero·ω_terminal²
```

— a cubic in `ω_terminal` whose single positive root is the top speed for that power
setting and that load. You don't have to solve it analytically; the simulation simply
integrates until `α → 0` and lands on it. But it's a great **test target**: pick a power,
run until settled, and assert the measured terminal ω matches the root of that balance
equation (and never exceeds the safety cap `ω_max`).

### Coast-down (power cut)

Set `throttle = 0` ⇒ `τ_motor = 0`, leaving only the losses:

```
I · α = −(C_coulomb·sign(ω) + C_viscous·ω + k_aero·ω·|ω|)
```

ω decays monotonically to zero, and `E_kin = ½Iω²` decreases the whole way (doc 2 §2.4).
The Coulomb term guarantees the ride actually *stops* in finite time rather than
asymptotically creeping — the viscous and drag terms alone would only approach zero.

---

## 3.5 The E-stop ramp (not an instant halt)

An emergency stop on a real ride carrying 32 people **cannot** yank ω to zero — that would
be a lethal deceleration. The E-stop instead commands a **controlled decel ramp** over
`≈ 8 s` (default): the motor is driven in reverse / regenerative braking within a torque
limit so that |α| stays inside a human-safe bound, and the ride is walked down to rest
before entering the `Faulted` state. Model it as a bounded target-ω ramp, not a
`ω = 0` assignment. (State-machine details: Plan §6.)

---

## 3.6 Thermal side-model (optional flavour)

Sustained torque heats the motor; it cools toward ambient when idle:

```
dT/dt = ( P_loss − k·(T − T_ambient) ) / C
```

where `P_loss` is the electrical/mechanical loss (roughly the friction power `τ_loss·ω`
plus motor inefficiency), `k` a cooling coefficient, `C` the thermal mass. A first-order
lag: temperature rises under load, plateaus, and decays exponentially when the drive
relaxes. Drives the `drive[*].motorTempC` and `bearing[*].tempC` telemetry channels and
becomes a fault source (overheating) later.

---

## 3.7 What this doc gives the test suite

| Behaviour | Assertion |
|-----------|-----------|
| No divide-by-zero at standstill | `τ_motor` finite at ω = 0 (the `ω_eps` guard) |
| Power → speed is correct | measured terminal ω matches the balance-equation root |
| Load slows the ramp | heavier load ⇒ longer time-to-cruise at fixed power |
| Terminal speed respects the cap | settled ω ≤ `ω_max` for any throttle |
| Coast-down is monotone & terminating | ω decreases to exactly 0 (Coulomb term) |
| E-stop is survivable | |α| during E-stop ramp stays within the safe bound |

Next: the carts that have **no** motor and swing on their own — [passive cart dynamics](04-passive-cart-dynamics.md).
