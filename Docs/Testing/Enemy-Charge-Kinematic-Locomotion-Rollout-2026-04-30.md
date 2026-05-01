# Enemy Charge Kinematic Locomotion Rollout - 2026-04-30

This rollout is guarded by `GameplayRuntimeFeatureFlags.EnableEnemyChargeKinematicLocomotion`.

## Behavior

- Flag off: Charge active movement keeps the legacy discrete `TickEntityMotionKind.ChargeMove` presentation path.
- Flag on: Charge active one-cell steps use `UnitKinematicRuntimeState` with `MotionMode.Charge`.
- Flag on: Charge active steps must not reach the legacy `TickEntityMotionKind.ChargeMove` path.
- Charge active start now waits for any non-settled ordinary enemy `MotionMode.Voluntary` kinematic movement to settle before entering Charge.
- Charge windup, recover, target selection, and passive contact rules are unchanged once Charge actually starts.
- Charge active step duration is `EnemyChargeTimingSettings.ActiveStepCooldownTicks`, normalized to an even value of at least 2 ticks.
- Anchor commit happens at `totalTicks / 2`; passive contact can only resolve after that commit.
- Charge kinematic presentation uses `TickKinematicMotionTrack` plus `TickEnemyChargePresentationSignal`; legacy `ChargeMove` is not emitted for the kinematic path.
- `MoveEntity` midpoint anchor commit is classified as a grid transaction primitive, not legacy ordinary Unit movement.
- Legacy `ChargeMove` remains retained only for flag-off baseline. Legacy grid transactions for box/action/topology/spawn/respawn/cleanup are not removed by this rollout.
- Boundary v1 suppresses legacy `ChargeMove` only for kinematic charge anchor commits and ordinary Unit locomotion boundaries. `BoxActionMovement`, topology, spawn, respawn, cleanup, scripted relocation, and flag-off legacy grid transactions keep their required presentation.
- Boundary metadata is trace diagnostic data and must remain outside canonical replay hashes.
- During settle-wait, the enemy remains in Patrol/Chase, ordinary kinematic presentation continues from its current local offset, and Charge windup/active presentation is not emitted.
- After ordinary settle, ChargeStart is re-evaluated from the current snapshot. If the target moved out of a valid same-face row/column lane, if the first step is blocked, or if the enemy is no longer controllable, Charge does not start.

## Rollback

Set `EnableEnemyChargeKinematicLocomotion` to false. This does not require disabling `EnableEnemySameFaceContinuousLocomotion`.

When ordinary enemy kinematic locomotion is enabled but Charge kinematic locomotion is disabled, ChargeStart still waits for non-settled ordinary voluntary kinematic movement to settle. The legacy `ChargeMove` path starts only after the revalidated ChargeStart transition.

## Known Risks

- Passive contact from Charge reaches the player at the midpoint commit instead of the legacy same-tick move.
- Charge can feel less responsive by up to the remaining ordinary kinematic settle time.
- Revalidation after settle can cancel ChargeStart if the target moves or the lane becomes blocked during the wait.
- Replay hashes change in flag-on Charge scenarios because `UnitKinematics` now contains `MotionMode.Charge`.
