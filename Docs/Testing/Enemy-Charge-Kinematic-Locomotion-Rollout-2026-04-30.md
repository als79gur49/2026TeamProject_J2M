# Enemy Charge Kinematic Locomotion Rollout - 2026-04-30

This rollout is guarded by `GameplayRuntimeFeatureFlags.EnableEnemyChargeKinematicLocomotion`.

## Behavior

- Flag off: Charge active movement keeps the legacy discrete `TickEntityMotionKind.ChargeMove` presentation path.
- Flag on: Charge active one-cell steps use `UnitKinematicRuntimeState` with `MotionMode.Charge`.
- Charge windup, recover, AI mode selection, target selection, and passive contact rules are unchanged.
- Charge active step duration is `EnemyChargeTimingSettings.ActiveStepCooldownTicks`, normalized to an even value of at least 2 ticks.
- Anchor commit happens at `totalTicks / 2`; passive contact can only resolve after that commit.
- Charge kinematic presentation uses `TickKinematicMotionTrack` plus `TickEnemyChargePresentationSignal`; legacy `ChargeMove` is not emitted for the kinematic path.

## Rollback

Set `EnableEnemyChargeKinematicLocomotion` to false. This does not require disabling `EnableEnemySameFaceContinuousLocomotion`.

When ordinary enemy kinematic locomotion is enabled but Charge kinematic locomotion is disabled, active Charge clears stale ordinary voluntary kinematic residue before legacy Charge intent collection. This keeps the legacy Charge path from being suppressed by a previous Patrol/Chase kinematic step.

## Known Risks

- Passive contact from Charge reaches the player at the midpoint commit instead of the legacy same-tick move.
- Clearing stale ordinary kinematic residue at Charge start can visually snap if the previous ordinary move was mid-presentation.
- Replay hashes change in flag-on Charge scenarios because `UnitKinematics` now contains `MotionMode.Charge`.
