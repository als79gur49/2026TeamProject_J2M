# Legacy Ordinary Unit Movement Deprecation Phase 7: Fallback Cleanup Readiness

## Decision

Phase 7 does not delete additional runtime branches. `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline` remains in v1, but its meaning is now diagnostic compatibility only: it authorizes no covered player, enemy, or Charge fallback after Phase 6. Player attempts reject with `PlayerLegacyFallbackRemovedFromRuntime`, enemy attempts reject with `EnemyLegacyFallbackRemovedFromRuntime`, and Charge attempts reject with `ChargeLegacyFallbackRemovedFromRuntime`.

`MoveEntity`, `MovementExpander`, retained grid transactions, `TickEntityMotionKind.Move`, `TickEntityMotionKind.ChargeMove`, and default/flag-off glide retained fallback are not deletion targets in this phase. Golden and replay assets are not rewritten.

## Preset And Flag Readiness

| symbol | Phase 7 meaning | action |
|---|---|---|
| `RemovedLegacyFallbackDiagnosticBaseline` | Phase 8B canonical diagnostic compatibility preset for deterministic removed-fallback diagnostics | use for new removed-diagnostic tests |
| `LegacyOrdinaryFallbackBaseline` | deprecated compatibility alias for `RemovedLegacyFallbackDiagnosticBaseline` | keep in v1; future removal candidate after migration approval |
| `EnableLegacyOrdinaryUnitFallback` | compatibility diagnostic field that lets removed-specific reasons surface instead of the explicit-baseline gate | keep in v1; future options are keep, rename to `EnableRemovedLegacyFallbackDiagnostics`, or remove after replay migration approval |
| `LegacyOrdinaryFallbackEnabled` | compatibility alias with misleading fallback-enabled wording | keep in v1; inventory as Phase 8 cleanup candidate |

`EnableLegacyOrdinaryUnitFallback=true` does not make covered fallback legal. It only changes diagnostic routing: `false` produces `LegacyOrdinaryFallbackRequiresExplicitBaseline`; `true` proceeds to the removed player/enemy/Charge reason. Retained grid transaction behavior and glide policy are separate.

## Helper And Test Migration

Canonical helper names for new tests are:

- `AssertCoveredFallbackRemovedDiagnostics`
- `AssertPlayerFallbackRemovedFromRuntime`
- `AssertEnemyFallbackRemovedFromRuntime`
- `AssertChargeFallbackRemovedFromRuntime`

Older `Allows*` helper names remain only as obsolete compatibility wrappers. Phase 8A removes internal use of covered-fallback wrappers while keeping their definitions for compatibility; wrapper deletion is a later Phase 8B+ candidate. New test names should use `Removed`, `Rejected`, `Diagnostic`, or `Compatibility` wording instead of `Allowed`, `StillAllowed`, or `FlagOffBaseline` for covered fallback policy.

Phase 7 canaries:

- `Phase7_LegacyFallbackBaseline_IsDiagnosticCompatibilityPreset`
- `Phase7_None_NoCoveredFallback`
- `Phase7_DefaultGameplay_NoCoveredFallback`
- `Phase7_GridTransactionsRemainAllowed`
- `Phase7_GlidePolicyUnchanged`
- `Phase7_HelperNames_AreCurrent`
- `Replay_Phase7_LegacyFallbackBaseline_DiagnosticsDeterministic`
- `Replay_Phase7_DefaultGameplay_NoCoveredFallback`

## Presentation Cleanup Inventory

| presentation kind | still needed? | retained context | cleanup status |
|---|---:|---|---|
| `TickEntityMotionKind.Move` | yes | box item/action movement, topology handoff, retained grid transactions, non-covered movement | cleanup candidate only after retained presentation ownership is narrowed |
| `TickEntityMotionKind.ChargeMove` | maybe historical only | active non-kinematic Charge move presentation | cleanup candidate after golden/replay ownership approval |
| box/action/topology/spawn/respawn/script relocation presentation | yes | retained grid and lifecycle paths | not a Phase 7 deletion target |
| `TickKinematicMotionTrack` | yes | enemy ordinary, Charge, and flag-on glide replacement presentation | retained |
| `TickContinuousLocomotionTrack` | yes | player Free2D/stoppable replacement presentation | retained |
| `TickEnemyChargePresentationSignal` | yes | kinematic Charge presentation | retained |

## Replay And Golden Checklist

- Phase 6 and Phase 7 replay canaries keep removed diagnostics deterministic.
- No golden files are rewritten in Phase 7.
- Historical fallback output migration remains future owner-approved work.
- `RemovedLegacyFallbackDiagnosticBaseline` is the canonical replay/migration preset after Phase 8B; `LegacyOrdinaryFallbackBaseline` remains as a compatibility alias until removal approval.

## Next Phase Candidates

- Remove misleading helper wrappers after Phase 8A internal usage cleanup and external compatibility approval.
- Decide whether `LegacyOrdinaryFallbackBaseline` compatibility alias should be retained or removed.
- Decide whether `EnableLegacyOrdinaryUnitFallback` remains a compatibility field, becomes `EnableRemovedLegacyFallbackDiagnostics`, or is removed.
- Inventory retained uses of `TickEntityMotionKind.Move` and `TickEntityMotionKind.ChargeMove` before any presentation cleanup.
