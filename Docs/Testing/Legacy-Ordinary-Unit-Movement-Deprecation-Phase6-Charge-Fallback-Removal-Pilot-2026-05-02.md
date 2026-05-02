# Legacy Ordinary Unit Movement Deprecation Phase 6: Charge Fallback Removal Pilot

Date: 2026-05-02

## Decision

Phase 6 removes Charge active legacy fallback authorization from the runtime path. A Charge active `MoveIntent` that reaches legacy expansion is rejected even under `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline`.

This is a Charge-only removal pilot. Player ordinary fallback remains removed from Phase 4, enemy ordinary fallback remains removed from Phase 5, and default/flag-off glide fallback remains a retained exception.

## Runtime Policy

`TickPipeline.ValidateLegacyExpansionIntents` remains the enforcement point.

- `DefaultGameplayLocomotion` and Charge kinematic-on lanes keep `ChargeCoveredKinematicReachedLegacyExpansion` for synthetic leaks.
- `GameplayRuntimeFeatureFlags.None` keeps the Phase 3 `LegacyOrdinaryFallbackRequiresExplicitBaseline` reason.
- `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline` now rejects Charge active fallback with `ChargeLegacyFallbackRemovedFromRuntime`.
- Player ordinary fallback continues to reject with `PlayerLegacyFallbackRemovedFromRuntime`.
- Enemy ordinary fallback continues to reject with `EnemyLegacyFallbackRemovedFromRuntime`.

Retained grid transactions still pass the grid transaction allowlist before fallback removal applies. Topology handoff, box/action materialization, spawn, respawn, cleanup, scripted relocation, anchor normalization, `MoveEntity`, and the `MovementExpander` grid branch are not deletion targets.

## Test Contract

Phase 6 canonical canaries:

- `Phase6_LegacyOrdinaryFallbackBaseline_ChargeFallbackRemoved`
- `Phase6_LegacyBaseline_PlayerEnemyStillRemoved`
- `Phase6_LegacyBaseline_PlayerEnemyChargeRemoved`
- `Phase6_None_ChargeFallbackStillBlocked`
- `Phase6_DefaultGameplay_ChargeFallbackStillAbsent`
- `Phase6_ChargeKinematicFlagOn_NoLegacyChargeMove`
- `Phase6_GlideFallbackPolicyUnchanged`
- `Phase6_MovementExpander_GridBranchStillAllowed`
- `Replay_Phase6_LegacyBaseline_PlayerEnemyChargeRemoved`
- `Replay_Phase6_ChargeLegacyBaseline_FallbackRemoved`
- `Replay_Phase6_DefaultGameplay_NoChargeMove`

The Phase 2C/Phase 3/Phase 5 Charge fallback-allowed canaries are superseded. Historical wrappers may remain only when they assert the Phase 6 removed policy.

## Replay and Golden Policy

Phase 6 does not rewrite golden files. Replay tests assert that player, enemy, and Charge covered fallback are absent under the explicit legacy baseline while diagnostics remain deterministic.

Golden migration for historical Charge fallback output remains a future owner-approved phase.

## Next Phase Candidates

- Cleanup or removal of `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline` after replay/migration ownership is settled.
- Stale fallback helper cleanup after Phase 6 diagnostics are stable.
- Legacy `TickEntityMotionKind.Move` / `TickEntityMotionKind.ChargeMove` presentation cleanup after retained grid presentation and golden policy are approved.
