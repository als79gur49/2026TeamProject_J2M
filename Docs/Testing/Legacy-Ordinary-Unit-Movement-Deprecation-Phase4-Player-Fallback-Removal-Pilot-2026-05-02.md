# Legacy Ordinary Unit Movement Deprecation Phase 4: Player Fallback Removal Pilot

Date: 2026-05-02

## Decision

Phase 4 removes player legacy ordinary fallback authorization from the runtime path. A player ordinary `MoveIntent` that reaches legacy expansion is rejected even under `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline`.

This is a player-only pilot. Enemy ordinary fallback, Charge active fallback, default/flag-off glide fallback, retained grid transactions, `MoveEntity`, and `MovementExpander` remain supported.

## Runtime Policy

`TickPipeline.ValidateLegacyExpansionIntents` is the Phase 4 enforcement point.

- `DefaultGameplayLocomotion`, Free2D-on, and player kinematic-on lanes keep the existing `PlayerCoveredLocomotionReachedLegacyExpansion` leak reason.
- `GameplayRuntimeFeatureFlags.None` keeps the Phase 3 `LegacyOrdinaryFallbackRequiresExplicitBaseline` reason.
- `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline` now rejects player ordinary fallback with `PlayerLegacyFallbackRemovedFromRuntime`.
- Enemy ordinary fallback and Charge active fallback remain allowed when `EnableLegacyOrdinaryUnitFallback` is true.

Retained grid transactions still pass the grid transaction allowlist before player fallback removal applies. Topology handoff, box/action materialization, spawn, respawn, cleanup, scripted relocation, anchor normalization, `MoveEntity`, and the `MovementExpander` grid branch are not deletion targets.

## Test Contract

Phase 4 canonical canaries:

- `Phase4_LegacyOrdinaryFallbackBaseline_PlayerFallbackRemoved`
- `Phase4_None_PlayerFallbackStillBlocked`
- `Phase4_DefaultGameplay_PlayerFallbackStillAbsent`
- `Phase4_PlayerKinematicFlagOn_NoLegacyFallback`
- `Phase4_PlayerTopologyHandoff_StillGridTransaction`
- `Phase4_MovementExpander_GridBranchStillAllowed`
- `Phase4_LegacyBaseline_EnemyFallbackStillAllowed`
- `Phase4_LegacyBaseline_ChargeFallbackStillAllowed`
- `Replay_Phase4_LegacyBaseline_PlayerRemovedEnemyChargeRetained`
- `Replay_Phase4_LegacyBaseline_PlayerFallbackRemoved`

Old player fallback-allowed canaries are superseded. Enemy and Charge fallback-allowed canaries remain valid.

## Replay and Golden Policy

Phase 4 does not rewrite golden files. Replay tests assert that the player fallback is absent under the explicit legacy baseline while enemy and Charge fallback remain deterministic and present in scenarios that include them.

Golden migration for historical player fallback output remains a future owner-approved phase.

## Next Phase Candidates

- Enemy ordinary fallback removal pilot.
- Charge active fallback removal pilot.
- Player fallback presentation/helper cleanup after the Phase 4 diagnostic is stable.
