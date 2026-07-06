## Context

Every driven body (the mill and its four hubs) advances through the shared semi-implicit-Euler integrator in `RotationalDynamics.Integrate`, which subtracts a loss torque

```
τ_loss(ω) = C_coulomb·sign(ω) + C_viscous·|ω| + k_aero·ω·|ω|
```

from the drive torque each tick (docs 02 §2.1 and 03 §3.3). When power is cut the drive term is zero, so the rig coasts down on `τ_loss` alone. The integrator is correct — Coulomb friction gives a clean dead-band stop, and the coast guard prevents losses from reversing the spin — but the **coefficients are far too small for the rig's inertia**:

| Body | Current C_coulomb | C_viscous | k_aero | Approx. inertia (empty → full) |
|------|------------------:|----------:|-------:|-------------------------------:|
| Mill | 800 | 1,500 | 5,000 | ~1.65×10⁵ → ~3×10⁵ kg·m² |
| Hub  | 100 | 200 | 150 | ~3×10³ → ~6.5×10³ kg·m² |

The mill's inertia is dominated by four hub assemblies orbiting at 6 m (doc 02 §2.3). Against ~1.65×10⁵ kg·m², the low-speed loss torque is tiny: below ~1 rad/s the aero term (∝ ω²) has faded and only Coulomb (800 N·m) is left, giving a deceleration of just 800 / 165 000 ≈ 0.005 rad/s². From a slow creep that is well over a minute to the rest threshold — exactly the "keeps spinning for a very long time" the operator reports.

## Goals / Non-Goals

**Goals:**
- A de-powered mill (empty and fully loaded) coasts to `IsAtRest` within a realistic, bounded window (target **8–45 s** from cruise), with the mill getting the largest relative increase.
- The hubs settle promptly too, so the whole rig reaches `IsAtRest` without one body creeping on after the others.
- Pin the new behaviour with a coast-down duration test so the tuning cannot silently regress.
- Preserve the existing guarantees: coast-down energy stays monotonically non-increasing, the over-speed cap is never exceeded, and the powered cruise speed stays high enough to keep the ride's intended felt G.

**Non-Goals:**
- No change to the integrator, the loss *formula*, or the motor/torque model — only the coefficient *values* change.
- No new brake-torque actuator. Braking in this model remains "cut the drive power and coast" (`Ride.BrakeEngines` / `CutAllPower`); this change makes that coast realistic, it does not add active braking.
- No telemetry, API, state-machine, or frontend changes.

## Decisions

### Decision 1 — Retune the six loss constants only; leave `RotationalDynamics` untouched

The integrator already brings a coasting body cleanly to rest; the defect is purely a magnitude problem. Changing only the named constants in `RideParameters` keeps the change small, reviewable, and fully covered by the existing property tests plus one new duration test.

_Alternatives considered:_ adding an explicit brake-torque term for the stopping phase (rejected — larger surface, and the docs deliberately model braking as power-cut coast); making losses scale with inertia at runtime (rejected — unphysical, and hides the tuning in code rather than in named constants).

### Decision 2 — Bias the increase toward Coulomb and viscous friction, bump aero only modestly

The symptom (a long low-speed tail) lives where **Coulomb and viscous** dominate. Aerodynamic drag (∝ ω²) is already the *largest* loss at cruise — for the mill it is ~31 kN·m of the ~36 kN·m the motor can deliver at 2.5 rad/s — so there is almost no headroom to raise `k_aero` without dropping the powered terminal speed (and, since felt G ∝ ω², dropping the felt G noticeably). Raising Coulomb gives a firm, load-independent stop torque that guarantees a bounded stop; raising viscous bleeds the mid-speed range; a small aero bump shaves the top of the coast without gutting cruise.

_Alternative considered:_ a large aero increase to make the *high-speed* part of the coast drop fast. Rejected as the primary lever — it barely touches the low-speed tail that is the actual complaint, and it moves cruise/G the most.

### Decision 3 — Recommended starting values (final numbers tuned against the test)

| Body | Constant | Current | Proposed | ×    |
|------|----------|--------:|---------:|-----:|
| Mill | `MillCoulombFriction` | 800   | **5,000** | 6.3× |
| Mill | `MillViscousFriction` | 1,500 | **3,500** | 2.3× |
| Mill | `MillAeroDrag`        | 5,000 | **6,000** | 1.2× |
| Hub  | `HubCoulombFriction`  | 100   | **400**   | 4.0× |
| Hub  | `HubViscousFriction`  | 200   | **500**   | 2.5× |
| Hub  | `HubAeroDrag`         | 150   | **200**   | 1.3× |

Back-of-envelope with these values: the mill's powered terminal speed settles at roughly **~2.2 rad/s** (was pinned at the 2.5 cap) — a modest ~12 % cruise reduction, so felt G stays within its intended band — while the empty-mill coast from cruise to rest drops from well over a minute to roughly **~25–35 s**. These are *starting points*; the implementer tunes them against the new coast-down test and the terminal/G check (Decision 4), raising `MillCoulombFriction` first if the fully-loaded coast (larger inertia, so the longest coast) overruns the 45 s ceiling.

### Decision 4 — Guard with a coast-down duration test, and re-verify the existing physics tests

Add a test to `DrivenBodyTests` that spins the mill to steady state, cuts power, counts steps to `IsAtRest`, and asserts the elapsed time falls inside the 8–45 s window for both an empty and a fully loaded rig. The existing `Kinetic_energy_never_increases_while_coasting_down` and `Power_drives_the_mill_up_to_but_not_past_the_over_speed_cap` tests must continue to pass unchanged (they encode the energy-honesty and cap guarantees). The heavier-mill-is-slower and different-load-different-speed tests are load-relative and remain valid.

## Risks / Trade-offs

- **Powered cruise speed and felt G drop with higher losses** → keep the aero bump small (Decision 2), target only a ~10–15 % cruise reduction, and add an assertion that cruise stays a substantial fraction of the over-speed cap so the ride doesn't quietly lose its punch. If the golden-scenario G target (docs appendix: ~2–3 g at full ride) drifts out of range, trim viscous/aero before Coulomb.
- **Fully loaded rig is the binding case for the upper bound** (largest inertia → longest coast under a fixed Coulomb torque) → the duration test covers the fully loaded rig explicitly; if it overruns 45 s, raise `MillCoulombFriction` (load-independent stop torque) rather than aero.
- **Too-aggressive Coulomb could read as an unrealistic hard stop / risk chatter near ω = 0** → the integrator already clamps within a dead-band around zero, and the test's lower bound (must not stop in under 8 s) catches an over-stiff choice.
- **Docs drift** → update `docs/03` §3.3/§3.4 and `docs/appendix-parameters.md` in the same change so the golden-scenario baseline matches the shipped constants.
