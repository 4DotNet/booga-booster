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

## Loss-model coefficients (tune to taste)

Not fixed by the plan — pick values that give the sanity-check behaviour below, then lock
them in as the golden-scenario baseline.

| Parameter | Symbol | Role |
|-----------|--------|------|
| Coulomb friction | `C_coulomb` | constant bearing drag (sets a clean stop) |
| Viscous friction | `C_viscous` | speed-proportional bearing drag |
| Aerodynamic drag | `k_aero` | ω² air resistance (sets terminal speed) |
| Motor speed floor | `ω_eps` | ~1e-3 rad/s, divide-by-zero guard (doc 3 §3.1) |

## Sanity check

With these parameters, a **full ride** (~2.4 t of carts + people orbiting at 6 m):

- has a mill moment of inertia around **2×10⁵ kg·m²**,
- reaches cruise in **~15–25 s** on 90 kW,
- and produces **~2–3 g** at the seats.

If your numbers land far outside those ranges, something in the inertia model (doc 2) or
the motor/loss balance (doc 3) is off.
