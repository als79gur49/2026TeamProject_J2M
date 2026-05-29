# Enemy Same-Face Kinematic Locomotion Rollout - 2026-04-30

This rollout is guarded by `GameplayRuntimeFeatureFlags.EnableEnemySameFaceContinuousLocomotion`.

## Contract

- Flag off: enemy ordinary ground movement keeps the legacy immediate `MoveCommitted` anchor update and same-tick passive contact behavior.
- Flag on: enemy ordinary same-face one-cell ground movement uses `UnitKinematicRuntimeState` and `TickKinematicMotionTrack`.
- Flag on: eligible enemy ordinary ground movement must not reach the legacy ordinary `MoveIntent` -> `MovementExpander` -> `TickEntityMotionKind.Move` path.
- `MoveEntity` midpoint anchor commit is classified as a grid transaction primitive, not legacy ordinary Unit movement.
- Timing reuses `PlayerKinematicLocomotionTimingSettings`: default 60 TPS resolves to 20 ticks per cell with midpoint anchor commit on tick 10.
- Passive contact rules are unchanged. Contact timing changes only because the enemy semantic anchor commits at midpoint.
- Charge, jump, phase relocation, topology transitions, push, flip, item, projectile, and forced motion are not migrated in this slice. Active glide chase movement has a separate opt-in slice guarded by `EnableEnemyGlideKinematicLocomotion`; it reuses enemy kinematic machinery but is not enabled by this flag.
- Legacy grid transactions remain retained for box/action/topology/spawn/respawn/cleanup and flag-off fallback.
- Boundary v1 classifies enemy anchor commits as `LocomotionAnchorCommit` and suppresses duplicate legacy entity motion only for locomotion boundaries. Grid transactions such as `BoxActionMovement`, `TopologyMaterialization`, and `SpawnRespawnPlacement` keep required legacy presentation.
- Boundary metadata is diagnostic and must not affect canonical replay hashes.
- Boundary v1 stabilization adds `TickPipeline_ValidateLegacyExpansionIntents_BlocksFlagOnUnitOrdinaryMove` and `Replay_NoUnexpectedLegacyUnitOrdinaryMovementDetected` canaries for enemy ordinary kinematic movement. The Phase 1 targeted Unity XML canaries are runtime green. Any flag-on enemy ordinary `Move` reaching `MovementExpander` is a regression.
- `Boundary_UnknownInventory_NormalGameplayHasNoUnexpectedUnknownMovement` covers representative retained grid transactions so normal movement/finalization traces do not leave meaningful enemy-adjacent movement as `Boundary=Unknown`.

## Rollback

Set `EnableEnemySameFaceContinuousLocomotion` to false or pass `GameplayRuntimeFeatureFlags.None`.
The legacy `MovementExpander` path remains present for retained grid transactions and passive contact tests. Explicit baseline tests now cover deterministic removed diagnostics, not fallback output.
`GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion` enables this path for readiness canaries. As of legacy ordinary movement deprecation Phase 3, `None` is no longer the explicit legacy fallback baseline; removed-diagnostic compatibility tests use `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline` without authorizing covered fallback.
`GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion` now includes `EnableEnemyGlideKinematicLocomotion`; active glide default adoption uses the enemy kinematic lane while preserving the explicit flag-off rollback baseline.
Default bundle adoption is not legacy deletion. Phase 1 of the deletion-readiness gate isolates covered player/enemy/Charge/glide locomotion fallback under default/flag-on lanes, and Phase 3 moved covered fallback diagnostics to an explicit baseline while keeping `MoveEntity`, `MovementExpander`, retained grid transactions, and flag-off active glide fallback out of the deletion target. Phase 5 removes enemy ordinary fallback authorization from runtime, and Phase 6 removes Charge fallback authorization too. Current removed diagnostics should use `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline`; use the kinematic replacement paths instead.
Scoped deletion preparation pins enemy ordinary fallback removal with `ScopedDeletionPrep_EnemyLegacyFallback_RemovedByPhase5` and the default/replay no-covered fallback canaries.

## Expected Test Impact

- Enemy flag-on replay hashes may change while enemy `UnitKinematics` are active.
- Enemy explicit legacy baseline replay now asserts removed fallback diagnostics instead of legacy movement output.
- Player same-face kinematic tests are expected to remain unchanged unless the combined player/enemy factory is explicitly used.
