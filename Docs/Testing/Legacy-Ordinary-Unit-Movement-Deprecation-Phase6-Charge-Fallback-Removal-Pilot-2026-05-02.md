# Legacy Ordinary Unit Movement Deprecation Phase 6: Charge Fallback Removal Pilot

Date: 2026-05-02

## Decision

Phase 6 removes Charge active legacy fallback authorization from the runtime path. A Charge active `MoveIntent` that reaches legacy expansion is rejected even under `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline`.

This is a Charge-only removal pilot. Player ordinary fallback remains removed from Phase 4, enemy ordinary fallback remains removed from Phase 5, and after glide default adoption only flag-off glide fallback remains a retained exception.

## Runtime Policy

`TickPipeline.ValidateLegacyExpansionIntents` remains the enforcement point.

- `DefaultGameplayLocomotion` and Charge kinematic-on lanes keep `ChargeCoveredKinematicReachedLegacyExpansion` for synthetic leaks.
- `GameplayRuntimeFeatureFlags.None` keeps the Phase 3 `LegacyOrdinaryFallbackRequiresExplicitBaseline` reason.
- `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline` now rejects Charge active fallback with `ChargeLegacyFallbackRemovedFromRuntime`.
- Player ordinary fallback continues to reject with `PlayerLegacyFallbackRemovedFromRuntime`.
- Enemy ordinary fallback continues to reject with `EnemyLegacyFallbackRemovedFromRuntime`.

Phase 8B/8C adds `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline` as the canonical name for this removed-diagnostic preset. `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline` remains a deprecated compatibility alias with the same flag shape.

Retained grid transactions still pass the grid transaction allowlist before fallback removal applies. Topology handoff, box/action materialization, spawn, respawn, cleanup, scripted relocation, anchor normalization, `MoveEntity`, and the `MovementExpander` grid branch are not deletion targets.

## Test Contract

Phase 6 canonical canaries:

- `Phase6_RemovedDiagnosticBaseline_ChargeFallbackRemoved`
- `Phase6_RemovedDiagnosticBaseline_PlayerEnemyStillRemoved`
- `Phase6_RemovedDiagnosticBaseline_PlayerEnemyChargeRemoved`
- `Phase6_None_ChargeFallbackStillBlocked`
- `Phase6_DefaultGameplay_ChargeFallbackStillAbsent`
- `Phase6_ChargeKinematicFlagOn_NoLegacyChargeMove`
- `Phase6_GlideFlagOffFallbackStillRetained`
- `Phase6_MovementExpander_GridBranchStillAllowed`
- `Replay_Phase6_RemovedDiagnosticBaseline_PlayerEnemyChargeRemoved`
- `Replay_Phase6_ChargeRemovedDiagnosticBaseline_FallbackRemoved`
- `Replay_Phase6_DefaultGameplay_NoChargeMove`

The Phase 2C/Phase 3/Phase 5 Charge fallback-allowed canaries are superseded. Historical wrappers may remain only when they assert the Phase 6 removed policy.

## Replay and Golden Policy

Phase 6 does not rewrite golden files. Phase 8C replay tests assert that player, enemy, and Charge covered fallback are absent under `RemovedLegacyFallbackDiagnosticBaseline` while diagnostics remain deterministic.
ChargeMove presentation cleanup readiness adds inventory-to-action canaries after Phase 6: current runtime lanes must not output `ChargeMove`, while the enum and presentation consumers remain retained until presentation and replay/golden ownership approves deletion.
The ChargeMove producer isolation package removes `TickResultBuilder` inference from active Charge state to `ChargeMove` and locks default, `None`, diagnostic, charge-kinematic, and all-kinematic lanes as no-`ChargeMove`; it does not delete enum, timing, authoring, host consumers, replay vocabulary, or golden files.

Golden migration for historical Charge fallback output remains a future owner-approved phase.

## Next Phase Candidates

- Cleanup or removal of `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline` after replay/migration ownership is settled.
- Stale fallback helper cleanup after Phase 6 diagnostics are stable.
- Legacy `TickEntityMotionKind.Move` / `TickEntityMotionKind.ChargeMove` presentation cleanup after retained grid presentation and golden policy are approved.
