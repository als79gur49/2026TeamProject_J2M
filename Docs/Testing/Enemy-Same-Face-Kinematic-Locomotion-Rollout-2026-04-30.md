# Enemy Same-Face Kinematic Locomotion Rollout - 2026-04-30

This rollout is guarded by `GameplayRuntimeFeatureFlags.EnableEnemySameFaceContinuousLocomotion`.

## Contract

- Flag off: enemy ordinary ground movement keeps the legacy immediate `MoveCommitted` anchor update and same-tick passive contact behavior.
- Flag on: enemy ordinary same-face one-cell ground movement uses `UnitKinematicRuntimeState` and `TickKinematicMotionTrack`.
- Flag on: eligible enemy ordinary ground movement must not reach the legacy ordinary `MoveIntent` -> `MovementExpander` -> `TickEntityMotionKind.Move` path.
- `MoveEntity` midpoint anchor commit is classified as a grid transaction primitive, not legacy ordinary Unit movement.
- Timing reuses `PlayerKinematicLocomotionTimingSettings`: default 60 TPS resolves to 20 ticks per cell with midpoint anchor commit on tick 10.
- Passive contact rules are unchanged. Contact timing changes only because the enemy semantic anchor commits at midpoint.
- Charge, jump, glide, phase relocation, topology transitions, push, flip, item, projectile, and forced motion are not migrated in this slice.
- Legacy grid transactions remain retained for box/action/topology/spawn/respawn/cleanup and flag-off fallback.

## Rollback

Set `EnableEnemySameFaceContinuousLocomotion` to false or pass `GameplayRuntimeFeatureFlags.None`.
The legacy `MovementExpander` path remains present and is covered by the flag-off passive contact baseline.

## Expected Test Impact

- Enemy flag-on replay hashes may change while enemy `UnitKinematics` are active.
- Enemy flag-off replay and ordinary movement baselines should remain stable.
- Player same-face kinematic tests are expected to remain unchanged unless the combined player/enemy factory is explicitly used.
