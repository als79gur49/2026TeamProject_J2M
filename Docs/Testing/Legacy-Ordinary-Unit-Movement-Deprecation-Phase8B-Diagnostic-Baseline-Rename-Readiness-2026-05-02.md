# Legacy Ordinary Unit Movement Deprecation Phase 8B: Diagnostic Baseline Rename Readiness

## Decision

Phase 8B adds `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline` as the canonical diagnostic preset for deterministic removed-fallback diagnostics. `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline` remains as a deprecated compatibility alias and delegates to the new preset. Runtime validation semantics are unchanged: player, enemy, and Charge covered fallback attempts still reject with removed diagnostics, while `None` still rejects covered attempts with the explicit-baseline-required diagnostic.

`EnableLegacyOrdinaryUnitFallback`, `LegacyOrdinaryFallbackEnabled`, `TickPipeline`, `MovementExpander`, `MoveEntity`, retained grid transactions, `TickEntityMotionKind.Move`, `TickEntityMotionKind.ChargeMove`, glide retained fallback, and replay/golden assets are not renamed or changed in this phase. Phase 8D later adds `RemovedLegacyFallbackDiagnosticsEnabled` as the canonical helper while keeping `LegacyOrdinaryFallbackEnabled` as a deprecated compatibility alias.

## Preset Contract

| preset | Phase 8B role | legacy diagnostic field | replacement flags | behavior |
|---|---|---:|---:|---|
| `RemovedLegacyFallbackDiagnosticBaseline` | canonical diagnostic preset | true | false | deterministic player/enemy/Charge removed diagnostics |
| `LegacyOrdinaryFallbackBaseline` | deprecated compatibility alias | true | false | delegates to canonical diagnostic preset |
| `None` | unchanged no-advanced-locomotion lane | false | false | explicit-baseline-required diagnostic |
| `DefaultGameplayLocomotion` | unchanged default replacement bundle | false | true | replacement paths, no covered fallback |

## Test Coverage

Phase 8B adds canonical alias canaries:

- `Phase8B_RemovedDiagnosticBaseline_IsCanonicalAlias`
- `LegacyAliasCleanup_LegacyOrdinaryFallbackBaseline_IsRemoved`
- `Phase8B_RemovedDiagnosticBaseline_PlayerEnemyChargeDiagnostics`
- `Phase8B_DefaultGameplay_NoCoveredFallback_Unchanged`
- `Phase8B_None_NoCoveredFallback_Unchanged`
- `Phase8B_GridTransactionsRemainAllowed`
- `Phase8B_GlideDefaultAdoptionAndFlagOffFallbackRetained`
- `Replay_Phase8B_RemovedDiagnosticBaseline_DiagnosticsDeterministic`
- `Replay_Phase8B_DefaultGameplay_NoCoveredFallback`

Historical Phase 6, Phase 7, and Phase 8A tests remain as compatibility wrappers. New tests and docs should use `RemovedLegacyFallbackDiagnosticBaseline` when they need the removed-diagnostic preset.

Phase 8C completes the internal usage cleanup readiness pass: current tests, replay helpers, and current-policy docs use `RemovedLegacyFallbackDiagnosticBaseline`; `LegacyOrdinaryFallbackBaseline` remains a deprecated compatibility alias only for compatibility tests and historical documentation.

Phase 8D completes the helper naming readiness pass: current runtime/tests/docs use `RemovedLegacyFallbackDiagnosticsEnabled`; `LegacyOrdinaryFallbackEnabled` remains a deprecated compatibility alias.

## Replay And Golden

No replay or golden assets are rewritten. The new preset alias preserves the same flag shape as the old alias, and current traces record flag values rather than the static property name. Phase 8B replay tests assert deterministic traces and removed diagnostics under the new alias.

## Next Phase Candidates

- Keep monitoring that non-historical tests use `RemovedLegacyFallbackDiagnosticBaseline`.
- Decide whether to add `[Obsolete]` to `LegacyOrdinaryFallbackBaseline` after warning churn is acceptable.
- Decide whether `EnableLegacyOrdinaryUnitFallback` should remain a compatibility diagnostic field, be renamed to align with `RemovedLegacyFallbackDiagnosticsEnabled`, or be removed after replay migration.
