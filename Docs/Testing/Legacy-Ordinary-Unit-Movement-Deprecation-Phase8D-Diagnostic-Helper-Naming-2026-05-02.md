# Legacy Ordinary Unit Movement Deprecation Phase 8D: Diagnostic Helper Naming

Date: 2026-05-02

## Decision

Phase 8D added `GameplayRuntimeFeatureFlags.deleted legacy fallback diagnostic flag` as the canonical helper property for removed-fallback diagnostic routing. The old helper alias has since been removed. Phase 8E makes `GameplayRuntimeFeatureFlags.deleted legacy fallback diagnostic flag` the canonical runtime field and removes old-name compatibility projection.

Runtime validation semantics are unchanged. When the diagnostic helper is false, covered player, enemy, and Charge fallback attempts still reject with `LegacyOrdinaryFallbackRequiresExplicitBaseline`. When the diagnostic helper is true, those attempts route to `PlayerLegacyFallbackRemovedFromRuntime`, `EnemyLegacyFallbackRemovedFromRuntime`, or `ChargeLegacyFallbackRemovedFromRuntime`. No covered fallback is authorized.

`TickPipeline`, `MovementExpander`, `MoveEntity`, retained grid transactions, `TickEntityMotionKind.Move`, `TickEntityMotionKind.ChargeMove`, glide retained fallback, replay assets, and golden files are not semantically changed in Phase 8D.

## Helper Contract

| preset | field | canonical helper | compatibility helper | covered fallback result |
|---|---|---|---|---|
| `None` | `deleted legacy fallback diagnostic flag=false` | `deleted legacy fallback diagnostic flag=false` | removed | explicit-baseline-required diagnostic |
| `DefaultGameplayLocomotion` | `false` | `false` | `false` | no covered fallback |
| `removed diagnostic baseline preset (historical, deleted)` | `true` | `true` | `true` | removed-specific diagnostics only |
| `removed diagnostic baseline preset (historical, deleted)` | `true` | `true` | removed | removed-specific diagnostics only |
| `AllKinematicLocomotionEnabled` | `false` | `false` | `false` | no diagnostic compatibility routing |

## Field Rename Inventory

| symbol | usage kind | rename risk | Phase 8D action | future phase |
|---|---|---|---|---|
| `deleted legacy fallback diagnostic flag` | struct field and constructor parameter | high: config, scene, trace, and compatibility churn | keep as underlying field | inventoried in Phase 8E; future Option B/C/D decision |
| `deleted legacy fallback diagnostic flag` | canonical helper property | low | add and use for current policy | keep |
| `RemovedDiagnosticHelperAlias` | removed compatibility helper property | low: callers migrated | removed | none |
| `deleted legacy fallback diagnostic flag=` trace token | debug/replay output token | high: replay/golden churn | keep token text, read canonical helper | later trace vocabulary cleanup |

## Usage Policy

Current runtime, tests, replay helpers, and documentation should use `deleted legacy fallback diagnostic flag` when referring to removed-fallback diagnostic routing. It must not be described as fallback authorization.

## Tests

Phase 8D adds canaries for:

- `Phase8D_deleted legacy fallback diagnostic flag_IsCanonicalHelper`
- `Phase8D_RemovedDiagnosticHelperAlias_IsRemoved`
- `Phase8D_RemovedDiagnosticHelper_DoesNotAuthorizeFallback`
- `Phase8D_None_Default_AllKinematic_HelperFalse`
- `Phase8D_RemovedDiagnosticHelperAlias_IsRemoved`
- `Phase8D_CurrentPolicyDocs_UseRemovedDiagnosticHelper`
- `Phase8D_GridTransactionsRemainAllowed`
- `Phase8D_GlideDefaultAdoptionAndFlagOffFallbackRetained`
- `Replay_Phase8D_RemovedDiagnosticHelper_DiagnosticsDeterministic`

## Future Phase 8E Candidates

Phase 8E completed the field migration. Keep historical helper-alias mentions from becoming current API guidance.
- Decide whether the `deleted legacy fallback diagnostic flag=` trace token should be renamed after replay/golden churn is approved.

Phase 8E completes the C안 canonical field migration and keeps `deleted legacy fallback diagnostic flag` as the runtime field. No compatibility projection is retained.
