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
- If active ends while the current committed anchor overlaps a solid, the existing `EnemyGlidePhase.LandingPending` lifecycle starts or persists and movement remains suppressed until the solid overlap clears.
- Nonlethal hit during active glide kinematic movement interrupts the voluntary kinematic pose and moves the glide lifecycle into recovery; lethal hit/removal purges unit kinematics and glide state through cleanup.
- Glide kinematic continuation requires authoritative glide provenance: the voluntary segment must have started inside the recorded active window. Arbitrary pre-seeded `MotionMode.Voluntary` enemy state is not treated as glide continuation.
- `Windup`, `LandingPending`, and `Recovery` still suppress movement. `Cooldown` follows ordinary enemy movement policy and is not a glide-specific migration target.

## Stabilization v1.1

v1.1 stabilized `EnableEnemyGlideKinematicLocomotion` as an explicit flag before default adoption.
The stabilization gate adds coverage for contact timing, LandingPending on solid overlap, active-end non-settled continuation, hit/death cleanup, replay determinism, and voluntary-state provenance.
Explicit flag-on active glide is considered stabilized for the legacy ordinary movement blocker once these tests are green.

## Default Adoption v2

Glide default adoption v2 adds `EnableEnemyGlideKinematicLocomotion` to `GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion`.
Default gameplay now includes player modern locomotion, enemy ordinary kinematic locomotion, Charge kinematic locomotion, and active glide kinematic locomotion.
This is default bundle adoption, not fallback deletion.
`GameplayRuntimeFeatureFlags.None` and explicit flag-off configurations keep the active glide fallback as the rollback and historical baseline.
`AllKinematicLocomotionEnabled` continues to include glide.

`CombinedGameplayShowcaseInstaller` uses `DefaultGameplayLocomotion`, so active glide kinematic locomotion is enabled in the showcase through the default bundle.
Campaign and development gameplay hosts that apply `DefaultGameplayLocomotion` get active glide kinematic locomotion through the same bundle.
Replay harness defaults remain `GameplayRuntimeFeatureFlags.None`; replay and golden baselines must not be migrated to the default bundle by this readiness slice.
Actual legacy ordinary Unit movement deletion remains separate and not complete until replay/golden policy and broad validation are complete. Phase 1 runtime validation keeps flag-off active glide fallback as a retained exception while covered player/enemy/Charge fallback isolation is complete.
Scoped deletion preparation pins this retained flag-off exception with `ScopedDeletionPrep_GlideFallback_IsRetainedException`; it does not delete the explicit fallback path.

## Rollback

Set `EnableEnemyGlideKinematicLocomotion` to false or pass `GameplayRuntimeFeatureFlags.None`.
`GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion` includes this flag after default adoption v2.
Flag-off active glide chase fallback remains covered by `BoundaryInventory_GlideFlagOff_FallbackStillRetained`.

## Expected Test Impact

- Flag-on active glide replay hashes may include `UnitKinematics` while the segment is active.
- Boundary metadata and reason strings remain diagnostic and must not affect canonical hashes.
- Flag-on active glide must not emit legacy ordinary `TickEntityMotionKind.Move`.
- Default gameplay active glide replay hashes may change because default now records active glide `UnitKinematics`.
- Flag-off replay/golden baselines remain stable and document the rollback baseline.
- Default deletion readiness no longer treats active glide fallback as a default retained exception. Deprecation Phase 1 now treats only flag-off active glide legacy fallback as retained, while default and explicit `EnableEnemyGlideKinematicLocomotion` remain no-legacy canaries.
