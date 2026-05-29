# Legacy Ordinary Unit Movement Deprecation Phase 8C: Legacy Alias Usage Cleanup

Date: 2026-05-02

## Decision

Phase 8C changes tests and documentation only. `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline` is the canonical preset for deterministic removed-fallback diagnostics. `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline` remains a deprecated compatibility alias for `RemovedLegacyFallbackDiagnosticBaseline`; it is not deleted in this phase.

2026-05-29 supersession: the follow-up alias cleanup deletes the legacy preset alias after current callers moved to `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline`. `LegacyOrdinaryFallbackBaseline` alias has been removed.

Runtime validation semantics are unchanged. Covered player, enemy, and Charge fallback attempts reject with removed diagnostics under the canonical preset. `GameplayRuntimeFeatureFlags.None` still rejects covered fallback with `LegacyOrdinaryFallbackRequiresExplicitBaseline`. Phase 8D supersedes the remaining helper naming inventory by adding `RemovedLegacyFallbackDiagnosticsEnabled`; `LegacyOrdinaryFallbackEnabled` remains a deprecated compatibility alias, and `EnableLegacyOrdinaryUnitFallback` remains the underlying compatibility field.

2026-05-29 supersession: the follow-up helper alias cleanup deletes the legacy helper alias after current runtime/tests moved to `RemovedLegacyFallbackDiagnosticsEnabled`. `LegacyOrdinaryFallbackEnabled` alias has been removed.

`TickPipeline`, `MovementExpander`, `MoveEntity`, retained grid transactions, `TickEntityMotionKind.Move`, `TickEntityMotionKind.ChargeMove`, glide retained fallback, replay assets, and golden files are not changed in Phase 8C.

## Old Alias Usage Inventory

| usage location | usage kind | current text/symbol | canonical replacement | allowed to keep? | action |
|---|---|---|---|---|---|
| `GameplayRuntimeFeatureFlags.cs` | alias definition | `LegacyOrdinaryFallbackBaseline => RemovedLegacyFallbackDiagnosticBaseline` | none | yes | keep as compatibility alias |
| `BoundaryInventoryScenarioTests.cs` | alias equivalence test | `LegacyAliasCleanup_LegacyOrdinaryFallbackBaseline_IsRemoved` | none | yes | keep |
| `BoundaryInventoryScenarioTests.cs` | compatibility-only canary | `LegacyAliasCleanup_LegacyOrdinaryFallbackBaseline_HasNoActiveUsage` | none | yes | keep |
| scenario and replay current diagnostics | canonical usage | old alias direct calls | `RemovedLegacyFallbackDiagnosticBaseline` | no | migrated |
| retained grid transaction tests | unrelated retained behavior | old alias direct calls | canonical or default non-old lane | no | migrated away from old alias |
| docs current policy | current preset wording | old alias as current baseline | `RemovedLegacyFallbackDiagnosticBaseline` | no | update wording |
| docs historical notes | historical preset name | `LegacyOrdinaryFallbackBaseline` | none | yes | mark as deprecated compatibility alias |
| obsolete helper wrappers | compatibility wrapper | old helper names mentioning old alias | none | yes | keep definitions only |

## Canonical Migration Policy

New tests, replay helpers, reports, and current-policy documentation must use `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline` when they intentionally need deterministic removed diagnostics.

Allowed old alias contexts:

- runtime alias definition and delegate
- Phase 8B alias equivalence test
- Phase 8C compatibility-only canary
- historical documentation explaining the pre-Phase8B name
- obsolete compatibility wrapper messages

Disallowed wording patterns:

- old alias name paired with `allows fallback`
- old alias name paired with `is the baseline`
- legacy fallback baseline wording paired with `remains allowed`

## Test And Replay Coverage

Phase 8C adds or updates these canaries:

- `Phase8C_RemovedDiagnosticBaseline_IsCanonicalUsage`
- `LegacyAliasCleanup_LegacyOrdinaryFallbackBaseline_HasNoActiveUsage`
- `Phase8C_CurrentPolicyDocs_UseRemovedDiagnosticBaseline`
- `Phase8C_AllowedOldAliasUsage_IsLimited`
- `Phase8C_GridTransactionsRemainAllowed`
- `Phase8C_GlideDefaultAdoptionAndFlagOffFallbackRetained`
- `Replay_Phase8C_RemovedDiagnosticBaseline_DiagnosticsDeterministic`
- `Replay_Phase8C_DefaultGameplay_NoCoveredFallback`

The replay canaries compare deterministic trace/state output under `RemovedLegacyFallbackDiagnosticBaseline` and assert removed diagnostics without `Boundary=LegacyFallback`. No replay or golden artifact is rewritten.

## Phase 8D Follow-up

- `RemovedLegacyFallbackDiagnosticsEnabled` is the canonical helper property for removed diagnostic routing.
- `LegacyOrdinaryFallbackEnabled` remains a deprecated compatibility alias.
- `EnableLegacyOrdinaryUnitFallback` remains the underlying compatibility field and is a Phase 8E rename/delete candidate.
- Decide whether to remove `LegacyOrdinaryFallbackBaseline` after compatibility usage is quiet.
- Continue retained presentation inventory for `TickEntityMotionKind.Move` and `TickEntityMotionKind.ChargeMove` without changing retained grid transaction or glide policy.
