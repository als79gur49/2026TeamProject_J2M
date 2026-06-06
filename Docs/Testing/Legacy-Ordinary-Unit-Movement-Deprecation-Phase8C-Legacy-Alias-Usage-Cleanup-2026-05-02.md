# Legacy Ordinary Unit Movement Deprecation Phase 8C: Legacy Alias Usage Cleanup

Date: 2026-05-02

## Decision

Phase 8C changed tests and documentation only. `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline` is the canonical preset for deterministic removed-fallback diagnostics. The old `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline` alias has since been removed.

Runtime validation semantics are unchanged. Covered player, enemy, and Charge fallback attempts reject with removed diagnostics under the canonical preset. `GameplayRuntimeFeatureFlags.None` still rejects covered fallback with `LegacyOrdinaryFallbackRequiresExplicitBaseline`. Phase 8D supersedes the helper naming inventory with `RemovedLegacyFallbackDiagnosticsEnabled`; the old `LegacyOrdinaryFallbackEnabled` alias has since been removed, and `EnableLegacyOrdinaryUnitFallback` remains the underlying compatibility field.

`TickPipeline`, `MovementExpander`, `MoveEntity`, retained grid transactions, `TickEntityMotionKind.Move`, `TickEntityMotionKind.ChargeMove`, glide retained fallback, replay assets, and golden files are not changed in Phase 8C.

## Old Alias Usage Inventory

| usage location | usage kind | current text/symbol | canonical replacement | allowed to keep? | action |
|---|---|---|---|---|---|
| `GameplayRuntimeFeatureFlags.cs` | alias definition | `LegacyOrdinaryFallbackBaseline => RemovedLegacyFallbackDiagnosticBaseline` | `RemovedLegacyFallbackDiagnosticBaseline` | no | removed |
| `BoundaryInventoryScenarioTests.cs` | alias absence test | `Phase8C_LegacyOrdinaryFallbackBaseline_IsRemoved` | canonical absence canary | yes | keep |
| scenario and replay current diagnostics | canonical usage | old alias direct calls | `RemovedLegacyFallbackDiagnosticBaseline` | no | migrated |
| retained grid transaction tests | unrelated retained behavior | old alias direct calls | canonical or default non-old lane | no | migrated away from old alias |
| docs current policy | current preset wording | old alias as current baseline | `RemovedLegacyFallbackDiagnosticBaseline` | no | update wording |
| docs historical notes | historical preset name | `LegacyOrdinaryFallbackBaseline` | `RemovedLegacyFallbackDiagnosticBaseline` | yes | mark historical/removed |
| obsolete helper wrappers | compatibility wrapper | old helper names mentioning old alias | none | yes | keep definitions only |

## Canonical Migration Policy

New tests, replay helpers, reports, and current-policy documentation must use `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline` when they intentionally need deterministic removed diagnostics.

Allowed old alias contexts:

- historical documentation explaining the pre-Phase8B name
- obsolete compatibility wrapper messages

Disallowed wording patterns:

- old alias name paired with `allows fallback`
- old alias name paired with `is the baseline`
- legacy fallback baseline wording paired with `remains allowed`

## Test And Replay Coverage

Phase 8C adds or updates these canaries:

- `Phase8C_RemovedDiagnosticBaseline_IsCanonicalUsage`
- `Phase8C_LegacyOrdinaryFallbackBaseline_IsRemoved`
- `Phase8C_CurrentPolicyDocs_UseRemovedDiagnosticBaseline`
- `Phase8C_AllowedOldAliasUsage_IsLimited`
- `Phase8C_GridTransactionsRemainAllowed`
- `Phase8C_GlideDefaultAdoptionAndFlagOffFallbackRetained`
- `Replay_Phase8C_RemovedDiagnosticBaseline_DiagnosticsDeterministic`
- `Replay_Phase8C_DefaultGameplay_NoCoveredFallback`

The replay canaries compare deterministic trace/state output under `RemovedLegacyFallbackDiagnosticBaseline` and assert removed diagnostics without `Boundary=LegacyFallback`. No replay or golden artifact is rewritten.

## Phase 8D Follow-up

- `RemovedLegacyFallbackDiagnosticsEnabled` is the canonical helper property for removed diagnostic routing.
- `LegacyOrdinaryFallbackEnabled` has been removed; use `RemovedLegacyFallbackDiagnosticsEnabled`.
- `EnableLegacyOrdinaryUnitFallback` remains the underlying compatibility field and is a Phase 8E rename/delete candidate.
- `LegacyOrdinaryFallbackBaseline` has been removed; use `RemovedLegacyFallbackDiagnosticBaseline`.
- Continue retained presentation inventory for `TickEntityMotionKind.Move` and `TickEntityMotionKind.ChargeMove` without changing retained grid transaction or glide policy.
