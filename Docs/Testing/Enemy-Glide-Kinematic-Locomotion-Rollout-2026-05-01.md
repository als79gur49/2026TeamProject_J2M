# Enemy Glide Kinematic Locomotion Rollout - 2026-05-01

This rollout is guarded by `GameplayRuntimeFeatureFlags.EnableEnemyGlideKinematicLocomotion`.

## Contract

- Flag on: active glide chase `MovementCommandKind.Move` may enter the enemy kinematic lane when it is a same-face one-cell ground step from a settled pose.
- Flag on: the kinematic state uses `MotionMode.Voluntary`; glide semantics stay in `EnemyGlideRuntimeState.Active` and boundary/trace reason text.
- Flag on: horizontal presentation comes from `TickKinematicMotionTrack`; height presentation stays in `TickEnemyGlidePresentationSignal` and remains presentation-only.
- Flag on: active glide anchor commits are `MovementExecutionBoundaryKind.LocomotionAnchorCommit` with reason `GlideActiveKinematicAnchorCommit`.
- Active glide solid blocker bypass is preserved. Terrain, board edge, topology, reservation, and unit overlap restrictions are not bypassed.
- Contact remains anchor-based: source anchor before commit, destination anchor from commit tick onward. Swept and footprint contact are not introduced.
- If active ends while a kinematic segment is non-settled, the segment completes naturally. New glide kinematic steps start only while phase is `Active`.
- `Windup`, `LandingPending`, and `Recovery` still suppress movement. `Cooldown` follows ordinary enemy movement policy and is not a glide-specific migration target.

## Rollback

Set `EnableEnemyGlideKinematicLocomotion` to false or pass `GameplayRuntimeFeatureFlags.None`.
`GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion` intentionally does not include this flag in v1.
Flag-off active glide chase fallback remains covered by `BoundaryInventory_Glide_ActiveLegacyFallback_FlagOff_IsDocumented`.

## Expected Test Impact

- Flag-on active glide replay hashes may include `UnitKinematics` while the segment is active.
- Boundary metadata and reason strings remain diagnostic and must not affect canonical hashes.
- Flag-on active glide must not emit legacy ordinary `TickEntityMotionKind.Move`.
- Flag-off replay/golden baselines remain stable until default-bundle adoption is explicitly approved.
