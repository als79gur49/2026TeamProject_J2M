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

v1.1 keeps `EnableEnemyGlideKinematicLocomotion` explicit and still excludes it from `DefaultGameplayLocomotion`.
The stabilization gate adds coverage for contact timing, LandingPending on solid overlap, active-end non-settled continuation, hit/death cleanup, replay determinism, and voluntary-state provenance.
Explicit flag-on active glide is considered stabilized for the legacy ordinary movement blocker once these tests are green; default bundle adoption remains a later decision.

## Default Adoption Readiness v1

Glide default adoption v1 chooses Option B: keep `EnableEnemyGlideKinematicLocomotion` as an explicit opt-in and do not add it to `GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion`.
The v1.1 targeted green result is sufficient to mark explicit flag-on active glide as stable for the glide legacy ordinary blocker, but it is not sufficient to change the default gameplay bundle.
The remaining default-adoption risks are showcase behavior drift, broad-suite hidden regression, replay/golden churn, and ambiguity in no-legacy canaries if active glide is treated as default-covered before the bundle policy is approved.
`AllKinematicLocomotionEnabled` may still include glide for manual or broad canaries, but it is not the default gameplay policy.

`CombinedGameplayShowcaseInstaller` uses `DefaultGameplayLocomotion`, so glide kinematic locomotion is not enabled automatically in the showcase in this v1 decision.
Campaign and development gameplay hosts only get glide kinematic locomotion when they explicitly set `EnableEnemyGlideKinematicLocomotion`.
Replay harness defaults remain `GameplayRuntimeFeatureFlags.None`; replay and golden baselines must not be migrated to the default bundle by this readiness slice.
Actual legacy ordinary Unit movement deletion remains separate and blocked/partial until default adoption or an equivalent approved replacement, replay/golden policy, and broad validation are complete.

## Rollback

Set `EnableEnemyGlideKinematicLocomotion` to false or pass `GameplayRuntimeFeatureFlags.None`.
`GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion` intentionally does not include this flag in adoption v1.
Flag-off active glide chase fallback remains covered by `BoundaryInventory_Glide_ActiveLegacyFallback_FlagOff_IsDocumented`.

## Expected Test Impact

- Flag-on active glide replay hashes may include `UnitKinematics` while the segment is active.
- Boundary metadata and reason strings remain diagnostic and must not affect canonical hashes.
- Flag-on active glide must not emit legacy ordinary `TickEntityMotionKind.Move`.
- Flag-off replay/golden baselines remain stable until default-bundle adoption is explicitly approved.
- Default deletion readiness remains partial/blocked for glide because adoption v1 keeps the glide flag out of `DefaultGameplayLocomotion`. Deprecation Phase 1 therefore treats default/flag-off active glide legacy fallback as a retained exception, while explicit `EnableEnemyGlideKinematicLocomotion` remains a no-legacy canary.
