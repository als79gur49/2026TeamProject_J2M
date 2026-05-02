# Legacy Ordinary Unit Movement Deprecation Phase 5: Enemy Fallback Removal Pilot

Date: 2026-05-02

## Decision

Phase 5 removes enemy legacy ordinary fallback authorization from the runtime path. An enemy ordinary `MoveIntent` that reaches legacy expansion is rejected even under `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline`.

This is an enemy-only removal pilot. Player ordinary fallback remains removed from Phase 4, Charge active fallback was still supported under the explicit legacy baseline at Phase 5, and default/flag-off glide fallback remains a retained exception. Phase 6/7 supersede the Charge baseline allowance: `LegacyOrdinaryFallbackBaseline` is now diagnostic compatibility only.

Phase 6 supersedes the Charge portion of this status. Charge active fallback is now rejected under `LegacyOrdinaryFallbackBaseline` with `ChargeLegacyFallbackRemovedFromRuntime`.

## Runtime Policy

`TickPipeline.ValidateLegacyExpansionIntents` remains the enforcement point.

- `DefaultGameplayLocomotion` and enemy kinematic-on lanes keep `EnemyCoveredOrdinaryKinematicReachedLegacyExpansion` for synthetic leaks.
- `GameplayRuntimeFeatureFlags.None` keeps the Phase 3 `LegacyOrdinaryFallbackRequiresExplicitBaseline` reason.
- `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline` now rejects enemy ordinary fallback with `EnemyLegacyFallbackRemovedFromRuntime`.
- Player ordinary fallback continues to reject with `PlayerLegacyFallbackRemovedFromRuntime`.
- At Phase 5, Charge active fallback remained allowed when `EnableLegacyOrdinaryUnitFallback` was true. Phase 6 supersedes this allowance.

Retained grid transactions still pass the grid transaction allowlist before fallback removal applies. Topology handoff, box/action materialization, spawn, respawn, cleanup, scripted relocation, anchor normalization, `MoveEntity`, and the `MovementExpander` grid branch are not deletion targets.

## Test Contract

Phase 5 canonical canaries:

- `Phase5_LegacyOrdinaryFallbackBaseline_EnemyFallbackRemoved`
- `Phase6_LegacyBaseline_PlayerEnemyChargeRemoved`
- `Phase5_LegacyBaseline_EnemyFallbackRemovedByPhase5`
- `Phase5_LegacyBaseline_ChargeFallbackRemovedByPhase6`
- `Replay_Phase6_LegacyBaseline_PlayerEnemyChargeRemoved`
- `Replay_Phase5_EnemyLegacyBaseline_FallbackRemoved`

The Phase 2B/Phase 4 enemy fallback-allowed canaries are superseded. Phase 6 supersedes Charge fallback-allowed canaries.

## Replay and Golden Policy

Phase 5 does not rewrite golden files. Phase 6 replay tests assert that player, enemy, and Charge fallback are absent under the explicit legacy baseline while diagnostics remain deterministic.

Golden migration for historical enemy fallback output remains a future owner-approved phase.

## Next Phase Candidates

- Charge active fallback removal pilot.
- Enemy fallback presentation/helper cleanup after the Phase 5 diagnostic is stable.
- Player fallback presentation/helper cleanup after Phase 4/5 replay stability is confirmed.
