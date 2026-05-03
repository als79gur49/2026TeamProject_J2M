# Legacy Ordinary Unit Movement Deprecation Phase 8D: Diagnostic Helper Naming

Date: 2026-05-02

## Decision

Phase 8D adds `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticsEnabled` as the canonical helper property for removed-fallback diagnostic routing compatibility. `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackEnabled` remains a deprecated compatibility alias and delegates to the canonical helper. `GameplayRuntimeFeatureFlags.EnableLegacyOrdinaryUnitFallback` remains the underlying compatibility field; it is not renamed or deleted in this phase.

Runtime validation semantics are unchanged. When the diagnostic helper is false, covered player, enemy, and Charge fallback attempts still reject with `LegacyOrdinaryFallbackRequiresExplicitBaseline`. When the diagnostic helper is true, those attempts route to `PlayerLegacyFallbackRemovedFromRuntime`, `EnemyLegacyFallbackRemovedFromRuntime`, or `ChargeLegacyFallbackRemovedFromRuntime`. No covered fallback is authorized.

`TickPipeline`, `MovementExpander`, `MoveEntity`, retained grid transactions, `TickEntityMotionKind.Move`, `TickEntityMotionKind.ChargeMove`, glide retained fallback, replay assets, and golden files are not semantically changed in Phase 8D.

## Helper Contract

| preset | field | canonical helper | compatibility helper | covered fallback result |
|---|---|---|---|---|
| `None` | `EnableLegacyOrdinaryUnitFallback=false` | `RemovedLegacyFallbackDiagnosticsEnabled=false` | `LegacyOrdinaryFallbackEnabled=false` | explicit-baseline-required diagnostic |
| `DefaultGameplayLocomotion` | `false` | `false` | `false` | no covered fallback |
| `RemovedLegacyFallbackDiagnosticBaseline` | `true` | `true` | `true` | removed-specific diagnostics only |
| `LegacyOrdinaryFallbackBaseline` | `true` | `true` | `true` | deprecated preset alias to canonical diagnostics |
| `AllKinematicLocomotionEnabled` | `false` | `false` | `false` | no diagnostic compatibility routing |

## Field Rename Inventory

| symbol | usage kind | rename risk | Phase 8D action | future phase |
|---|---|---|---|---|
| `EnableLegacyOrdinaryUnitFallback` | struct field and constructor parameter | high: config, scene, trace, and compatibility churn | keep as underlying field | inventoried in Phase 8E; future Option B/C/D decision |
| `RemovedLegacyFallbackDiagnosticsEnabled` | canonical helper property | low | add and use for current policy | keep |
| `LegacyOrdinaryFallbackEnabled` | deprecated compatibility helper property | medium: misleading name | keep as delegate to canonical helper | removal or obsolete warning candidate |
| `LegacyFallback=` trace token | debug/replay output token | high: replay/golden churn | keep token text, read canonical helper | later trace vocabulary cleanup |

## Usage Policy

Current runtime, tests, replay helpers, and documentation should use `RemovedLegacyFallbackDiagnosticsEnabled` when referring to removed-fallback diagnostic routing. `LegacyOrdinaryFallbackEnabled` may appear only in its compatibility alias definition, compatibility alias tests, and historical documentation. `EnableLegacyOrdinaryUnitFallback` may appear as the data field name and in field rename inventory, but it must not be described as fallback authorization.

## Tests

Phase 8D adds canaries for:

- `Phase8D_RemovedLegacyFallbackDiagnosticsEnabled_IsCanonicalHelper`
- `Phase8D_LegacyOrdinaryFallbackEnabled_IsCompatibilityAlias`
- `Phase8D_RemovedDiagnosticHelper_DoesNotAuthorizeFallback`
- `Phase8D_None_Default_AllKinematic_HelperFalse`
- `Phase8D_LegacyOrdinaryFallbackEnabled_HasNoCanonicalInternalUsage`
- `Phase8D_CurrentPolicyDocs_UseRemovedDiagnosticHelper`
- `Phase8D_GridTransactionsRemainAllowed`
- `Phase8D_GlideDefaultAdoptionAndFlagOffFallbackRetained`
- `Replay_Phase8D_RemovedDiagnosticHelper_DiagnosticsDeterministic`

## Future Phase 8E Candidates

- Decide whether `EnableLegacyOrdinaryUnitFallback` should be renamed to a removed-diagnostic compatibility field or retained indefinitely for compatibility.
- Decide whether `LegacyOrdinaryFallbackEnabled` should receive `[Obsolete]` after warning churn is acceptable.
- Decide whether the `LegacyFallback=` trace token should be renamed after replay/golden churn is approved.

Phase 8E completes the field readiness inventory and keeps `EnableLegacyOrdinaryUnitFallback` unchanged. A future phase may consider adding a canonical field as a compatibility projection, but Phase 8E does not implement that option.
