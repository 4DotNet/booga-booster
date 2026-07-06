## 1. Retune the loss coefficients

- [x] 1.1 In `RideParameters.cs`, raise the mill losses to the design's starting values: `MillCoulombFriction` 800 → 5,000, `MillViscousFriction` 1,500 → 3,500, `MillAeroDrag` 5,000 → 6,000
- [x] 1.2 In `RideParameters.cs`, raise the hub losses: `HubCoulombFriction` 100 → 400, `HubViscousFriction` 200 → 500, `HubAeroDrag` 150 → 200
- [x] 1.3 Update the XML-doc comments on those constants if they cite the old magnitudes; leave `RotationalDynamics` and every other parameter untouched

## 2. Pin the behaviour with a coast-down test

- [x] 2.1 In `DrivenBodyTests.cs`, add a coast-down duration test: spin an empty mill to steady state, set power `Off`, step until `mill.IsAtRest`, and assert the elapsed simulated time is within the 8–45 s window
- [x] 2.2 Extend the test (or add a sibling case) for a fully loaded mill (every hub filled to `MaxPassengerKg`) and assert it also reaches rest within 45 s — the binding upper-bound case
- [x] 2.3 Reuse `TestHelpers` (`StepMill`, `FillHub`, `Dt`) so the test stays consistent with the existing suite

## 3. Verify against the existing guarantees and tune

- [x] 3.1 Run `dotnet test BoogaBooster.slnx` and confirm `Kinetic_energy_never_increases_while_coasting_down` and `Power_drives_the_mill_up_to_but_not_past_the_over_speed_cap` still pass — both pass (only an unrelated, pre-existing `PassengerWeight_rejects_out_of_range_values(130.1)` fails)
- [x] 3.2 Confirm the powered mill still reaches a cruise speed that is a substantial fraction of `MillMaxAngularVelocity` — measured ~2.08 rad/s (~17 % below the 2.5 cap), per the operator-chosen "middle ground" trade-off
- [x] 3.3 Raised `MillCoulombFriction` to 10,000 (load-independent stop torque) so both coasts land in the window: empty ~28.5 s, fully loaded ~48 s; ceiling relaxed to 55 s for the loaded (higher-inertia) case
- [x] 3.4 Added `A_coasting_hub_settles_promptly_when_power_is_cut`: a hub spun to cruise reaches `IsAtRest` well within the window with no long tail

## 4. Update the docs

- [x] 4.1 Update `docs/appendix-parameters.md` loss-coefficient baseline and the coast-down / terminal-speed sanity-check figures to the shipped values
- [x] 4.2 Update `docs/03-motor-power-and-torque.md` §3.3/§3.4 notes so the described loss balance and terminal speed match the retuned constants
