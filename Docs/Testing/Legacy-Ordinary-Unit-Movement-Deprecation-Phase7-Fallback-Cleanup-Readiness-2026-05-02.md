# Legacy Ordinary Unit Movement Deprecation Phase 7: Fallback Cleanup Readiness

## Decision

Phase 7 does not delete additional runtime branches. Its diagnostic preset is now `the removed diagnostic baseline preset (historical, deleted)` after Phase 8B/8C. The preset authorizes no covered player, enemy, or Charge fallback after Phase 6. Player attempts reject with `PlayerLegacyFallbackRemovedFromRuntime`, enemy attempts reject with `EnemyLegacyFallbackRemovedFromRuntime`, and Charge attempts reject with `ChargeLegacyFallbackRemovedFromRuntime`.

`MoveEntity`, `MovementExpander`, retained grid transactions, `TickEntityMotionKind.Move`, `TickEntityMotionKind.ChargeMove`, and flag-off glide retained fallback are not deletion targets in this phase. Golden and replay assets are not rewritten.

## Preset And Flag Readiness

| symbol | Phase 7 meaning | action |
|---|---|---|
| `removed diagnostic baseline preset (historical, deleted)` | Phase 8B canonical diagnostic compatibility preset for deterministic removed-fallback diagnostics | use for new removed-diagnostic tests |
| old diagnostic baseline alias vocabulary | removed compatibility alias for `removed diagnostic baseline preset (historical, deleted)` | historical-only after C안 migration |
| `deleted legacy fallback diagnostic flag` | canonical diagnostic field that lets removed-specific reasons surface instead of the explicit-baseline gate | retained as the canonical runtime field after Phase 8E |
| `deleted legacy fallback diagnostic flag` | Phase 8D canonical helper property for removed diagnostic routing | use for current policy |
| old diagnostic helper alias vocabulary | removed compatibility alias with misleading fallback-enabled wording | historical-only after C안 migration |

`deleted legacy fallback diagnostic flag=true` does not make covered fallback legal. Phase 8D names the current helper `deleted legacy fallback diagnostic flag`: `false` produces `LegacyOrdinaryFallbackRequiresExplicitBaseline`; `true` proceeds to the removed player/enemy/Charge reason. Retained grid transaction behavior and glide policy are separate.

## Helper And Test Migration

Canonical helper names for new tests are:

- `AssertCoveredFallbackRemovedDiagnostics`
- `AssertPlayerFallbackRemovedFromRuntime`
- `AssertEnemyFallbackRemovedFromRuntime`
- `AssertChargeFallbackRemovedFromRuntime`

The obsolete covered-fallback helper wrapper surface is deleted by the C안 final cleanup. Current tests use canonical removed-diagnostic helpers. New test names should use `Removed`, `Rejected`, `Diagnostic`, or `Compatibility` wording instead of `Allowed`, `StillAllowed`, or `FlagOffBaseline` for covered fallback policy.

Phase 7 canaries:

- `Phase7_LegacyFallbackBaseline_IsDiagnosticCompatibilityPreset`
- `Phase7_None_NoCoveredFallback`
- `Phase7_DefaultGameplay_NoCoveredFallback`
- `Phase7_GridTransactionsRemainAllowed`
- `Phase7_GlidePolicy_DefaultAdoptedAndFlagOffFallbackRetained`
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
- `removed diagnostic baseline preset (historical, deleted)` is the canonical replay/migration preset after Phase 8B/8C; old diagnostic baseline alias vocabulary is historical-only after C안 migration.

## Next Phase Candidates

- Remove misleading helper wrappers after Phase 8A internal usage cleanup and external compatibility approval.
- Keep `removed diagnostic baseline preset (historical, deleted)` canonical and do not restore old diagnostic baseline alias vocabulary.
- Phase 8E completes the diagnostics field migration: `deleted legacy fallback diagnostic flag` is the canonical runtime field.
- Inventory retained uses of `TickEntityMotionKind.Move` and `TickEntityMotionKind.ChargeMove` before any presentation cleanup.
