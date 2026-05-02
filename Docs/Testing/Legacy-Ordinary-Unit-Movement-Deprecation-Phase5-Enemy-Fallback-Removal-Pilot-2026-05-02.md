# Legacy Ordinary Unit Movement Deprecation Phase 5: Enemy Fallback Removal Pilot

Date: 2026-05-02

## Decision

Phase 5 removes enemy legacy ordinary fallback authorization from the runtime path. An enemy ordinary `MoveIntent` that reaches legacy expansion is rejected even under `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline`.

This is an enemy-only removal pilot. Player ordinary fallback remains removed from Phase 4, Charge active fallback remains supported under the explicit legacy baseline, and default/flag-off glide fallback remains a retained exception.

## Runtime Policy

`TickPipeline.ValidateLegacyExpansionIntents` remains the enforcement point.

- `DefaultGameplayLocomotion` and enemy kinematic-on lanes keep `EnemyCoveredOrdinaryKinematicReachedLegacyExpansion` for synthetic leaks.
- `GameplayRuntimeFeatureFlags.None` keeps the Phase 3 `LegacyOrdinaryFallbackRequiresExplicitBaseline` reason.
- `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline` now rejects enemy ordinary fallback with `EnemyLegacyFallbackRemovedFromRuntime`.
- Player ordinary fallback continues to reject with `PlayerLegacyFallbackRemovedFromRuntime`.
- Charge active fallback remains allowed when `EnableLegacyOrdinaryUnitFallback` is true.

Retained grid transactions still pass the grid transaction allowlist before fallback removal applies. Topology handoff, box/action materialization, spawn, respawn, cleanup, scripted relocation, anchor normalization, `MoveEntity`, and the `MovementExpander` grid branch are not deletion targets.

## Test Contract

Phase 5 canonical canaries:

- `Phase5_LegacyOrdinaryFallbackBaseline_EnemyFallbackRemoved`
- `Phase5_LegacyBaseline_PlayerEnemyRemovedChargeRetained`
- `Phase5_LegacyBaseline_EnemyFallbackRemovedByPhase5`
- `Phase5_LegacyBaseline_ChargeFallbackStillAllowed`
- `Replay_Phase5_LegacyBaseline_PlayerEnemyRemovedChargeRetained`
- `Replay_Phase5_EnemyLegacyBaseline_FallbackRemoved`

The Phase 2B/Phase 4 enemy fallback-allowed canaries are superseded. Charge fallback-allowed canaries remain valid.

## Replay and Golden Policy

Phase 5 does not rewrite golden files. Replay tests assert that player and enemy fallback are absent under the explicit legacy baseline while Charge fallback remains deterministic and present in scenarios that include it.

Golden migration for historical enemy fallback output remains a future owner-approved phase.

## Next Phase Candidates

- Charge active fallback removal pilot.
- Enemy fallback presentation/helper cleanup after the Phase 5 diagnostic is stable.
- Player fallback presentation/helper cleanup after Phase 4/5 replay stability is confirmed.
