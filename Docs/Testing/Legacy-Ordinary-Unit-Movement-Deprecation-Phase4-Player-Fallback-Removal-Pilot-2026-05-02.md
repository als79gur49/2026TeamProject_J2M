# Legacy Ordinary Unit Movement Deprecation Phase 4: Player Fallback Removal Pilot

Date: 2026-05-02

## Decision

Phase 4 removes player legacy ordinary fallback authorization from the runtime path. A player ordinary `MoveIntent` that reaches legacy expansion is rejected even under `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline`.

This is a player-only pilot. At Phase 4, enemy ordinary fallback and Charge active fallback remained supported; after glide default adoption, only flag-off glide fallback, retained grid transactions, `MoveEntity`, and `MovementExpander` remain supported.

Phase 5 supersedes the enemy portion of this status. Enemy ordinary fallback is now rejected under `LegacyOrdinaryFallbackBaseline` with `EnemyLegacyFallbackRemovedFromRuntime`.

Phase 6 supersedes the Charge portion of this status. Charge active fallback is now rejected under `LegacyOrdinaryFallbackBaseline` with `ChargeLegacyFallbackRemovedFromRuntime`.

Phase 8B/8C renames the current diagnostic preset to `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline`; `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline` remains a deprecated compatibility alias for historical Phase 4 tests.

## Runtime Policy

`TickPipeline.ValidateLegacyExpansionIntents` is the Phase 4 enforcement point.

- `DefaultGameplayLocomotion`, Free2D-on, and player kinematic-on lanes keep the existing `PlayerCoveredLocomotionReachedLegacyExpansion` leak reason.
- `GameplayRuntimeFeatureFlags.None` keeps the Phase 3 `LegacyOrdinaryFallbackRequiresExplicitBaseline` reason.
- `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline` now rejects player ordinary fallback with `PlayerLegacyFallbackRemovedFromRuntime`.
- At Phase 4, enemy ordinary fallback and Charge active fallback remained allowed when `EnableLegacyOrdinaryUnitFallback` was true. Phase 5/6 supersede those allowances.

Retained grid transactions still pass the grid transaction allowlist before player fallback removal applies. Topology handoff, box/action materialization, spawn, respawn, cleanup, scripted relocation, anchor normalization, `MoveEntity`, and the `MovementExpander` grid branch are not deletion targets.

## Test Contract

Phase 4 canonical canaries:

- `Phase4_RemovedDiagnosticBaseline_PlayerFallbackRemoved`
- `Phase4_None_PlayerFallbackStillBlocked`
- `Phase4_DefaultGameplay_PlayerFallbackStillAbsent`
- `Phase4_PlayerKinematicFlagOn_NoLegacyFallback`
- `Phase4_PlayerTopologyHandoff_StillGridTransaction`

`Phase4_PlayerTopologyHandoff_StillGridTransaction` documents the compatibility path. Native Player Free2D topology transition is gated separately by `EnablePlayerFree2DNativeTopologyTransition` and uses `Free2DTopologyTransition` boundary metadata when enabled.
- `Phase4_MovementExpander_GridBranchStillAllowed`
- `Phase5_LegacyBaseline_EnemyFallbackRemovedByPhase5`
- `Phase4_LegacyBaseline_ChargeFallbackRemovedByPhase6`
- `Replay_Phase6_LegacyBaseline_PlayerEnemyChargeRemoved`
- `Replay_Phase4_LegacyBaseline_PlayerFallbackRemoved`

Old player fallback-allowed canaries are superseded. Enemy fallback-allowed canaries are superseded by Phase 5. Charge fallback-allowed canaries are superseded by Phase 6.

## Replay and Golden Policy

Phase 4 does not rewrite golden files. Phase 8C replay tests assert that player, enemy, and Charge fallback are absent under `RemovedLegacyFallbackDiagnosticBaseline` while diagnostics remain deterministic.

Golden migration for historical player fallback output remains a future owner-approved phase.

## Next Phase Candidates

- Enemy ordinary fallback removal pilot.
- Charge active fallback removal pilot.
- Player fallback presentation/helper cleanup after the Phase 4 diagnostic is stable.
