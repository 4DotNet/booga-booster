# Appendix — Default Parameters

The starting configuration for the physics engine. These are tuned to give coherent,
playable numbers — a believable big flat-ride — not to model a specific real machine.
Tune to taste; every one of them should be a named constant / config value, never a magic
number buried in the physics.

## Masses & geometry

| Parameter | Symbol | Default | Unit |
|-----------|--------|--------:|------|
| Passenger mass | `m_pax` | 75 | kg |
| Empty cart mass | `m_cart` | 150 | kg |
| Hub structure mass | `m_hub` | 400 | kg |
| Mill arm mass | `m_arm` | 800 | kg |
| Mill arm length | `L_mill` | 6 | m |
| Hub arm length | `L_hub` | 2 | m |
| Seat offset | `r_seat` | 0.5 | m |
| Pivot→CoM offset (empty cart) | `r_g0` | 0.35 | m |
| Cart yaw inertia (empty, about pivot) | `I_cart_cm` | 220 | kg·m² |

## Dynamics & drive

| Parameter | Symbol | Default | Unit |
|-----------|--------|--------:|------|
| Timestep | `dt` | 1/120 | s |
| Telemetry rate | — | 30 | Hz |
| Mill stall torque | `τ_stall` | 60,000 | N·m |
| Mill max power | `P_max` | 90 | kW |
| Cart pivot damping | `c` | 900 | N·m·s/rad |
| Gravity | `g` | 9.81 | m/s² |

## Safety limits

| Parameter | Symbol | Default | Unit |
|-----------|--------|--------:|------|
| Max allowed G | `G_max` | 4.5 | g |
| Over-speed cap (mill) | `ω_max` | 2.5 | rad/s |
| Imbalance dispatch limit | — | 50 | mm |
| Wind cutoff | `windMax` | 18 | m/s |
| E-stop decel ramp | — | 8 | s |

## Loss-model coefficients

Not fixed by the plan — picked to give the sanity-check behaviour below and locked in as the
golden-scenario baseline. The coefficients are sized against each body's rotating inertia so
a de-powered rig coasts to rest in a believable time (doc 3 §3.4); the increase is biased
toward Coulomb/viscous friction (which govern the low-speed tail) over aerodynamic drag
(which mostly sets terminal speed).

| Parameter | Symbol | Role | Mill | Hub |
|-----------|--------|------|-----:|----:|
| Coulomb friction | `C_coulomb` | constant bearing drag (sets a clean, bounded-time stop) | 10,000 | 400 |
| Viscous friction | `C_viscous` | speed-proportional bearing drag | 3,500 | 500 |
| Aerodynamic drag | `k_aero` | ω² air resistance (sets terminal speed) | 6,000 | 200 |
| Motor speed floor | `ω_eps` | ~1e-3 rad/s, divide-by-zero guard (doc 3 §3.1) | 1e-3 | 1e-3 |

Units: `C_coulomb` in N·m, `C_viscous` in N·m·s/rad, `k_aero` in N·m·s²/rad².

## Passenger experience

Derived in [doc 6](06-passenger-experience.md). Ratings are on a 0–100 scale
(`MinExperienceRating = 0`, `MaxExperienceRating = 100`). The queue-side constants live on a
`QueuePatience` domain service; the ride-side constants live in `RideParameters`.

| Parameter | Symbol | Default | Unit | Lives in |
|-----------|--------|--------:|------|----------|
| Queue patience grace period | `w_g` | 5 | min | `QueuePatience.GracePeriod` |
| Queue patience time constant | `τ_q` | 10 | min | `QueuePatience.DecayTimeConstant` |
| Intensity reference / episode threshold | `G_max` | 4.5 | g | `RideParameters.MaxGForce` |
| Happiness peak gain rate | `k_h` | 1.5 | pt/s | `RideParameters` |
| Happiness match width (Gaussian σ) | `σ_h` | 12 | pt | `RideParameters` |
| Nausea excess threshold | `Δ_n` | 30 | pt | `RideParameters` |
| Nausea base rate | `r_n` | 1 | pt/s | `RideParameters` |
| Nausea growth rate | `λ_n` | 0.15 | 1/s | `RideParameters` |
| Max-G episode duration | `T_ep` | 1 | s | `RideParameters` |
| Max-G nausea penalty | `P_ep` | 25 | pt | `RideParameters` |
| Initial happiness | `H₀` | U[65, 85] | pt | Queue (generator) |
| Initial preferred intensity | `P` | U[50, 100] | pt | Queue (generator) |
| Initial nausea | `N₀` | 0 | pt | Queue (generator) |
| Operator-boarded happiness | — | 75 | pt | `RideParameters.DefaultRiderHappiness` |
| Operator-boarded preferred intensity | — | 75 | pt | `RideParameters.DefaultRiderPreferredIntensity` |

### Sanity check

- A guest who waits **15 min** keeps `e^(−1) ≈ 37 %` of their arrival happiness (80 → 29).
- A rider at a **perfect intensity match** climbs from happiness 75 to 100 in
  `25 / 1.5 ≈ 17 s`.
- At a 30-point mismatch the happiness gain is `e^(−6.25) ≈ 0.2 %` of peak — negligible.
- A rider under **sustained excess** goes from nausea 0 to 100 in
  `(1/λ_n)·ln((100 + r_n/λ_n)/(r_n/λ_n)) = 6.67 · ln 16 ≈ 18.5 s`.
- At the hub speed cap (5 rad/s) the hub alone gives `2 m × (5 rad/s)² / 9.81 ≈ 5.1 g`, so max-G episodes
  are reachable.

## Sanity check

With these parameters, a **full ride** (~2.4 t of carts + people orbiting at 6 m):

- has a mill moment of inertia around **2×10⁵ kg·m²**,
- reaches cruise in **~15–25 s** on 90 kW,
- settles at a natural terminal speed of **~2.1 rad/s** — a little below the `ω_max = 2.5`
  safety cap, because the losses (dominated by aero drag at cruise) balance the motor
  before the cap; the cap is a ceiling, not the operating point,
- produces roughly **~2 g** at the seats at that terminal speed,
- and, once power is cut, **coasts to rest in ~30 s empty / ~50 s fully loaded** (the loaded
  rig carries ~2.5× the inertia, so it coasts longer).

If your numbers land far outside those ranges, something in the inertia model (doc 2) or
the motor/loss balance (doc 3) is off.
