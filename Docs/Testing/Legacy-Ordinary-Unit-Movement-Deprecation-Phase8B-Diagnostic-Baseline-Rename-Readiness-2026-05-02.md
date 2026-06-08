# Legacy Ordinary Unit Movement Deprecation Phase 8B: Diagnostic Baseline Rename Readiness

## Decision

Phase 8B adds `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline` as the canonical diagnostic preset for deterministic removed-fallback diagnostics. C안 later removes old diagnostic baseline alias vocabulary; runtime code accepts the canonical preset only. Runtime validation semantics are unchanged: player, enemy, and Charge covered fallback attempts still reject with removed diagnostics, while `None` still rejects covered attempts with the explicit-baseline-required diagnostic.

`RemovedLegacyFallbackDiagnosticsEnabled`, `TickPipeline`, `MovementExpander`, `MoveEntity`, retained grid transactions, `TickEntityMotionKind.Move`, `TickEntityMotionKind.ChargeMove`, glide retained fallback, and replay/golden assets are not renamed or changed in this phase. Phase 8D later adds `RemovedLegacyFallbackDiagnosticsEnabled` as the canonical helper.

## Preset Contract

| preset | Phase 8B role | legacy diagnostic field | replacement flags | behavior |
|---|---|---:|---:|---|
| `RemovedLegacyFallbackDiagnosticBaseline` | canonical diagnostic preset | true | false | deterministic player/enemy/Charge removed diagnostics |
| old diagnostic baseline alias vocabulary | removed compatibility alias | true | false | historical-only after C안 migration |
| `None` | unchanged no-advanced-locomotion lane | false | false | explicit-baseline-required diagnostic |
| `DefaultGameplayLocomotion` | unchanged default replacement bundle | false | true | replacement paths, no covered fallback |

## Test Coverage

Phase 8B adds canonical alias canaries:

- `Phase8B_RemovedDiagnosticBaseline_IsCanonicalAlias`
- `Phase8B_RemovedDiagnosticBaselineAlias_IsCompatibilityAlias`
- `Phase8B_RemovedDiagnosticBaseline_PlayerEnemyChargeDiagnostics`
- `Phase8B_DefaultGameplay_NoCoveredFallback_Unchanged`
- `Phase8B_None_NoCoveredFallback_Unchanged`
- `Phase8B_GridTransactionsRemainAllowed`
- `Phase8B_GlideDefaultAdoptionAndFlagOffFallbackRetained`
- `Replay_Phase8B_RemovedDiagnosticBaseline_DiagnosticsDeterministic`
- `Replay_Phase8B_DefaultGameplay_NoCoveredFallback`

Historical Phase 6, Phase 7, and Phase 8A tests remain as compatibility wrappers. New tests and docs should use `RemovedLegacyFallbackDiagnosticBaseline` when they need the removed-diagnostic preset.

Phase 8C completes the internal usage cleanup readiness pass: current tests, replay helpers, and current-policy docs use `RemovedLegacyFallbackDiagnosticBaseline`; old diagnostic baseline alias vocabulary is historical-only after C안 migration.

Phase 8D completes the helper naming readiness pass: current runtime/tests/docs use `RemovedLegacyFallbackDiagnosticsEnabled`; old helper alias vocabulary is historical-only after C안 migration.

## Replay And Golden

Replay and golden traces now use canonical removed-fallback diagnostics vocabulary. Current traces record canonical flag values rather than old static property names. Phase 8B replay tests assert deterministic traces and removed diagnostics under the canonical preset.

## Next Phase Candidates

- Keep monitoring that non-historical tests use `RemovedLegacyFallbackDiagnosticBaseline`.
- Keep `RemovedLegacyFallbackDiagnosticBaseline` canonical and do not restore old diagnostic baseline alias vocabulary.
- Decide whether `RemovedLegacyFallbackDiagnosticsEnabled` should remain a compatibility diagnostic field, be renamed to align with `RemovedLegacyFallbackDiagnosticsEnabled`, or be removed after replay migration.
